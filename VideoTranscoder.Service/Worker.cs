using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoTranscoder.Shared;

namespace VideoTranscoder.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private TranscoderSettings _settings;

        private string _currentOutputFile = null;
        private Process _currentHandBrakeProcess = null;
        private Process _currentFFmpegProcess = null;

        private DateTime _currentTranscodeStartTime;

        private readonly IHostApplicationLifetime _appLifetime;

        public Worker(
            ILogger<Worker> logger,
            IHostApplicationLifetime appLifetime)
        {
            _logger = logger;
            _appLifetime = appLifetime;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Video Transcoder Service started");
            LogToFile("========================================");
            LogToFile("Video Transcoder Service started");
            LogToFile("========================================");

            // Clear any previous stop requests
            ServiceControl.ClearStopRequest();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    // Check for stop signal
                    if (ServiceControl.IsStopRequested())
                    {
                        _logger.LogInformation("Stop requested by user");
                        LogToFile("Stop requested by user - cleaning up...");
                        CleanupCurrentOperation();
                        ServiceControl.ClearStopRequest();

                        // Wait a bit before continuing
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        //continue;

                        // SHUT DOWN THE HOST (this stops the Windows service)
                        _appLifetime.StopApplication();
                        return;
                    }

                    _settings = SettingsManager.Load();
                    LogToFile("Settings reloaded");

                    if (IsWithinScheduledWindow())
                    {
                        _logger.LogInformation("Within scheduled window - starting scan and transcode process");
                        LogToFile("Within scheduled window - starting scan");
                        ProcessVideos();

                        // Check for stop signal
                        if (ServiceControl.IsStopRequested())
                        {
                            return;
                        }

                        _logger.LogInformation("Videos processed. Next check will be in: " + _settings.CheckIntervalMinutes);
                        LogToFile("Videos processed. Next check will be in: " + _settings.CheckIntervalMinutes + " minutes.");

                        //TODO Improve logic. if IsStopRequested, service will not stop untill the delay has passed!
                        await Task.Delay(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), stoppingToken);
                    }
                    else
                    {
                        TimeSpan timeUntilNextWindow = CalculateTimeUntilNextWindow();
                        _logger.LogInformation("Outside scheduled window. Next window starts in {Time}",
                            FormatTimeSpan(timeUntilNextWindow));
                        LogToFile($"Outside scheduled window. Next window starts in {FormatTimeSpan(timeUntilNextWindow)}");

                        // Check for stop signal
                        if (ServiceControl.IsStopRequested())
                        {
                            return;
                        }
                     
                        TimeSpan waitTime = timeUntilNextWindow > TimeSpan.FromMinutes(5)
                            ? TimeSpan.FromMinutes(5)
                            : timeUntilNextWindow;

                        await Task.Delay(waitTime, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in service execution");
                    LogToFile($"Error in service execution: {ex.Message}", "ERROR");
                    LogToFile($"Stack trace: {ex.StackTrace}", "ERROR");

                    // Check for stop signal
                    if (ServiceControl.IsStopRequested())
                    {
                        return;
                    }

                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            LogToFile("========================================");
            LogToFile("Video Transcoder Service stopped");
            LogToFile("========================================");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service stopping...");
            LogToFile("Service stopping...");
            CleanupCurrentOperation();

            await base.StopAsync(cancellationToken);
        }


        private void CleanupCurrentOperation()
        {
            try
            {
                // Kill HandBrake process if running
                if (_currentHandBrakeProcess != null && !_currentHandBrakeProcess.HasExited)
                {
                    _logger.LogInformation("Killing HandBrake process");
                    LogToFile("Killing HandBrake process");
                    _currentHandBrakeProcess.Kill();
                    _currentHandBrakeProcess.WaitForExit();
                    _currentHandBrakeProcess = null;
                }

                // Kill FFmpeg process if running
                if (_currentFFmpegProcess != null && !_currentFFmpegProcess.HasExited)
                {
                    _logger.LogInformation("Killing FFmpeg process");
                    LogToFile("Killing FFmpeg process");
                    _currentFFmpegProcess.Kill();
                    _currentFFmpegProcess.WaitForExit();
                    _currentFFmpegProcess = null;
                }

                // Delete incomplete output file
                if (!string.IsNullOrEmpty(_currentOutputFile) && File.Exists(_currentOutputFile))
                {
                    _logger.LogInformation("Deleting incomplete output file: {File}", _currentOutputFile);
                    LogToFile($"Deleting incomplete output file: {_currentOutputFile}");

                    try
                    {
                        File.Delete(_currentOutputFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to delete output file: {Error}", ex.Message);
                        LogToFile($"Failed to delete output file: {ex.Message}", "WARNING");
                    }
                }

                // Clear progress
                ProgressManager.ClearProgress();

                _currentOutputFile = null;

                LogToFile("Cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                LogToFile($"Error during cleanup: {ex.Message}", "ERROR");
            }
        }

        private bool IsWithinScheduledWindow()
        {
            if (!_settings.RunOnSchedule)
                return true; // Always run if schedule is disabled

            // Check if today is a valid day
            DayOfWeek today = DateTime.Now.DayOfWeek;
            if (!_settings.IsValidDay(today))
            {
                _logger.LogDebug("Not running today ({Day})", today);
                return false;
            }

            TimeSpan now = DateTime.Now.TimeOfDay;
            TimeSpan start = _settings.ScheduleStartTime;
            TimeSpan end = _settings.ScheduleEndTime;

            // Handle overnight window (e.g., 10 PM to 6 AM)
            if (start > end)
            {
                // For overnight windows, check if the START day is valid
                // If current time is after start, we're in the same day as start
                // If current time is before end, we started yesterday
                if (now >= start)
                {
                    // We're in the evening of the start day
                    return true;
                }
                else if (now < end)
                {
                    // We're in the morning, started yesterday
                    // Check if yesterday was a valid day
                    DateTime yesterday = DateTime.Now.AddDays(-1);
                    return _settings.IsValidDay(yesterday.DayOfWeek);
                }
                return false;
            }
            // Handle same-day window (e.g., 2 AM to 6 AM)
            else
            {
                return now >= start && now < end;
            }
        }

        private TimeSpan CalculateTimeUntilNextWindow()
        {
            TimeSpan now = DateTime.Now.TimeOfDay;
            TimeSpan start = _settings.ScheduleStartTime;
            TimeSpan end = _settings.ScheduleEndTime;
            DateTime currentDate = DateTime.Now.Date;

            // Find the next valid day
            for (int daysAhead = 0; daysAhead < 7; daysAhead++)
            {
                DateTime checkDate = currentDate.AddDays(daysAhead);
                DayOfWeek checkDay = checkDate.DayOfWeek;

                if (!_settings.IsValidDay(checkDay))
                    continue; // Skip this day

                // If checking today
                if (daysAhead == 0)
                {
                    // If we're before the start time today
                    if (now < start)
                    {
                        return start - now;
                    }
                    // If window is overnight and we're after start
                    else if (start > end && now >= start)
                    {
                        return TimeSpan.Zero; // We're in the window now
                    }
                    // Otherwise, this day's window has passed, check tomorrow
                }
                else
                {
                    // Calculate time until start time on this future day
                    TimeSpan untilMidnight = TimeSpan.FromHours(24) - now;
                    TimeSpan additionalDays = TimeSpan.FromDays(daysAhead - 1);
                    return untilMidnight + additionalDays + start;
                }
            }

            // If no valid days found (all unchecked), wait 1 day and check again
            return TimeSpan.FromDays(1);
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
            else
            {
                return $"{ts.Minutes}m";
            }
        }

        private void ProcessVideos()
        {
            foreach (var dir in _settings.MonitoredDirectories)
            {
                // Check for stop signal
                if (ServiceControl.IsStopRequested())
                {
                    LogToFile("Stop requested during processing videos");
                    return;
                }

                if (!Directory.Exists(dir))
                {
                    _logger.LogWarning("Directory not found: {Directory}", dir);
                    continue;
                }

                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(f => _settings.FileExtensions.Contains(Path.GetExtension(f).ToLower()));

                foreach (var file in files)
                {

                    // Check for stop signal
                    if (ServiceControl.IsStopRequested())
                    {
                        LogToFile("Stop requested during processing video files");
                        return;
                    }
                    try
                    {
                        // Check if already transcoded
                        if (HistoryManager.IsAlreadyTranscoded(file))
                        {
                            _logger.LogInformation("Skipping already transcoded file: {File}", file);
                            continue;
                        }
                        if (ShouldTranscode(file))
                        {
                            TranscodeVideo(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing {File}", file);
                    }
                }
            }
        }

        private void ExtractSubtitles(string inputFile, string outputDirectory)
        {
            if (!_settings.ExtractSubtitles)
                return;

            if (!File.Exists(_settings.FFmpegPath))
            {
                _logger.LogWarning("FFmpeg not found at: {Path}", _settings.FFmpegPath);
                return;
            }

            try
            {


                // Report that we're starting subtitle extraction
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = true,
                    CurrentFile = inputFile,
                    Status = "Analyzing subtitles...",
                    SubtitleExtractionStatus = "Probing video file for subtitle streams"
                });

                // First, probe the file to see what subtitle streams exist
                var probeInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = $"-i \"{inputFile}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string probeOutput = "";
                using (var probeProcess = Process.Start(probeInfo))
                {
                    // Use StringBuilder to collect output asynchronously
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();

                    probeProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            outputBuilder.AppendLine(e.Data);
                        }
                    };

                    probeProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    probeProcess.BeginOutputReadLine();
                    probeProcess.BeginErrorReadLine();

                    probeProcess.WaitForExit();
                    probeProcess.WaitForExit(); // flush async output

                    // FFmpeg outputs info to stderr, not stdout
                    probeOutput = errorBuilder.ToString();
                }

                LogToFile($"Probe completed, analyzing subtitle streams");

                // Parse subtitle streams from ffmpeg output
                var subtitleStreams = new List<(int streamIndex, string language, string codec)>();
                var lines = probeOutput.Split('\n');

                foreach (var line in lines)
                {
                    if (line.Contains("Stream #") && line.Contains("Subtitle:"))
                    {
                        try
                        {
                            var streamPart = line.Split("Stream #")[1].Split(':');
                            int streamIndex = int.Parse(streamPart[1].Split('(')[0].Trim());

                            string language = "und";
                            if (line.Contains("(") && line.Contains(")"))
                            {
                                var langMatch = System.Text.RegularExpressions.Regex.Match(line, @"\(([a-z]{3})\)");
                                if (langMatch.Success)
                                    language = langMatch.Groups[1].Value;
                            }

                            string codec = "";
                            if (line.Contains("Subtitle:"))
                            {
                                var codecPart = line.Split("Subtitle:")[1].Trim().Split(' ')[0];
                                codec = codecPart.ToLower();
                            }

                            subtitleStreams.Add((streamIndex, language, codec));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to parse subtitle stream: {Error}", ex.Message);
                        }
                    }
                }

                if (subtitleStreams.Count == 0)
                {
                    _logger.LogInformation("No subtitles found in: {File}", inputFile);

                    ProgressManager.Save(new TranscodeProgress
                    {
                        IsTranscoding = true,
                        IsExtractingSubtitles = false,
                        CurrentFile = inputFile,
                        Status = "No subtitles found",
                        SubtitleExtractionStatus = "No subtitle streams detected"
                    });

                    return;
                }

                // Create subs folder
                string subsFolder = Path.Combine(outputDirectory, "subs");
                Directory.CreateDirectory(subsFolder);


                _logger.LogInformation("Found {Count} subtitle stream(s) in: {File}", subtitleStreams.Count, inputFile);

                // Update progress with total count
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = true,
                    CurrentFile = inputFile,
                    Status = "Extracting subtitles...",
                    SubtitleExtractionStatus = $"Found {subtitleStreams.Count} subtitle stream(s)",
                    TotalSubtitleStreams = subtitleStreams.Count,
                    ProcessedSubtitleStreams = 0
                });

                // Check if there's an English SRT subtitle
                bool hasEnglishSrt = subtitleStreams.Any(s =>
                    (s.language == "eng" || s.language == "en") &&
                    (s.codec == "subrip" || s.codec == "srt"));

                // Extract each subtitle stream
                var allowedFormats = _settings.GetSubtitleFormatsList();
                string baseFileName = Path.GetFileNameWithoutExtension(inputFile);
                var extractedFiles = new Dictionary<string, (string path, string language, string codec)>();

                for (int i = 0; i < subtitleStreams.Count; i++)
                {
                    var (streamIndex, language, codec) = subtitleStreams[i];

                    // Update progress
                    ProgressManager.Save(new TranscodeProgress
                    {
                        IsTranscoding = true,
                        IsExtractingSubtitles = true,
                        CurrentFile = inputFile,
                        Status = "Extracting subtitles...",
                        SubtitleExtractionStatus = $"Extracting subtitle {i + 1}/{subtitleStreams.Count} ({language}, {codec})",
                        TotalSubtitleStreams = subtitleStreams.Count,
                        ProcessedSubtitleStreams = i
                    });

                    // Determine output format based on user preferences
                    string outputFormat = DetermineOutputFormat(codec, allowedFormats);

                    // Build output filename
                    string subtitleFileName = subtitleStreams.Count > 1
                        ? $"{baseFileName}.{language}.{i}.{outputFormat}"
                        : $"{baseFileName}.{language}.{outputFormat}";

                    string subtitleOutputPath = Path.Combine(subsFolder, subtitleFileName);

                    // Extract subtitle
                    bool extracted = ExtractSingleSubtitle(inputFile, streamIndex, subtitleOutputPath, codec, outputFormat);

                    if (extracted)
                    {
                        extractedFiles[language] = (subtitleOutputPath, language, codec);
                    }
                }

                // Update progress after extraction
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = true,
                    CurrentFile = inputFile,
                    Status = "Extracting subtitles...",
                    SubtitleExtractionStatus = $"Extracted {extractedFiles.Count}/{subtitleStreams.Count} subtitle streams",
                    TotalSubtitleStreams = subtitleStreams.Count,
                    ProcessedSubtitleStreams = subtitleStreams.Count
                });

                // Convert English subtitle to SRT if no English SRT exists
                if (_settings.ConvertToSrtIfMissing && !hasEnglishSrt)
                {
                    // Update progress
                    ProgressManager.Save(new TranscodeProgress
                    {
                        IsTranscoding = true,
                        IsExtractingSubtitles = true,
                        CurrentFile = inputFile,
                        Status = "Converting subtitles...",
                        SubtitleExtractionStatus = "Converting to SRT format...",
                        TotalSubtitleStreams = subtitleStreams.Count,
                        ProcessedSubtitleStreams = subtitleStreams.Count
                    });

                    // Try to find English subtitle in extracted files
                    var englishSub = extractedFiles.FirstOrDefault(e =>
                        e.Key == "eng" || e.Key == "en" || e.Key == "und");

                    if (englishSub.Value.path != null)
                    {
                        _logger.LogInformation("No English SRT found. Converting English subtitle to SRT...");
                        ConvertSubtitleToSrt(englishSub.Value.path, subsFolder, baseFileName, englishSub.Value.language);
                    }
                    else if (extractedFiles.Count > 0)
                    {
                        // Fallback: convert first subtitle if no English found
                        var firstSub = extractedFiles.First().Value;
                        _logger.LogInformation("No English subtitles found. Converting first subtitle ({Language}) to SRT as fallback...", firstSub.language);
                        ConvertSubtitleToSrt(firstSub.path, subsFolder, baseFileName, firstSub.language);
                    }
                    else
                    {
                        _logger.LogInformation("No subtitles available to convert to SRT");
                    }
                }

                // Final progress update
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = false,
                    CurrentFile = inputFile,
                    Status = "Subtitle extraction complete",
                    SubtitleExtractionStatus = $"Completed: {extractedFiles.Count} subtitle(s) extracted",
                    TotalSubtitleStreams = subtitleStreams.Count,
                    ProcessedSubtitleStreams = subtitleStreams.Count
                });

                _logger.LogInformation("Subtitle extraction complete for: {File}", inputFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting subtitles from: {File}", inputFile);

                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = false,
                    CurrentFile = inputFile,
                    Status = "Subtitle extraction failed",
                    SubtitleExtractionStatus = $"Error: {ex.Message}"
                });
            }
        }

        private string DetermineOutputFormat(string codec, List<string> allowedFormats)
        {
            // Map common codec names to file extensions
            var codecMap = new Dictionary<string, string>
            {
                { "subrip", "srt" },
                { "srt", "srt" },
                { "ass", "ass" },
                { "ssa", "ass" },
                { "webvtt", "vtt" },
                { "vtt", "vtt" },
                { "mov_text", "srt" }, // MP4 text subtitles
                { "hdmv_pgs_subtitle", "sup" }, // Blu-ray
                { "dvd_subtitle", "sub" }
            };

            string defaultFormat = codecMap.ContainsKey(codec) ? codecMap[codec] : "srt";

            if (allowedFormats.Count == 0)
                return defaultFormat;

            // If codec matches one of the allowed formats, use it
            if (allowedFormats.Contains(defaultFormat))
                return defaultFormat;

            // Otherwise use first allowed format
            return allowedFormats[0];
        }

        private bool ExtractSingleSubtitle(string inputFile, int streamIndex, string outputPath, string codec, string outputFormat)
        {
            try
            {
                bool needsConversion = !IsNativeFormat(codec, outputFormat);

                string extractArgs;
                if (needsConversion)
                {
                    extractArgs = $"-i \"{inputFile}\" -map 0:{streamIndex} -f {GetFFmpegFormat(outputFormat)} \"{outputPath}\"";
                }
                else
                {
                    extractArgs = $"-i \"{inputFile}\" -map 0:{streamIndex} -c copy \"{outputPath}\"";
                }

                var extractInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = extractArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Extracting single subtitle stream {Index} to: {Output}", streamIndex, outputPath);
                LogToFile($"FFmpeg command: {_settings.FFmpegPath} {extractArgs}");

                using (var extractProcess = Process.Start(extractInfo))
                {
                    _currentFFmpegProcess = extractProcess; // Track the process

                    // Read output asynchronously to prevent deadlock
                    extractProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            //LogToFile(e.Data.Trim(), "FFMPEG"); do not log everything to file
                        }
                    };

                    extractProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            LogToFile(e.Data.Trim(), "FFMPEG");
                    };

                    extractProcess.BeginOutputReadLine();
                    extractProcess.BeginErrorReadLine();

                    extractProcess.WaitForExit(); // must be called twice
                    extractProcess.WaitForExit(); // flush async events

                    _currentFFmpegProcess = null;

                    if (extractProcess.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully extracted subtitle: {Output}", outputPath);
                        LogToFile($"Successfully extracted subtitle: {outputPath}", "SUCCESS");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract subtitle stream {Index}", streamIndex);
                        LogToFile($"Failed to extract subtitle stream {streamIndex}, exit code: {extractProcess.ExitCode}", "ERROR");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting subtitle stream {Index}", streamIndex);
                LogToFile($"Error extracting subtitle stream {streamIndex}: {ex.Message}", "ERROR");
                return false;
            }
        }

        private bool IsNativeFormat(string codec, string outputFormat)
        {
            // Check if the codec already matches the output format (no conversion needed)
            var nativeMap = new Dictionary<string, string>
    {
        { "subrip", "srt" },
        { "srt", "srt" },
        { "ass", "ass" },
        { "ssa", "ass" },
        { "webvtt", "vtt" },
        { "vtt", "vtt" }
    };

            return nativeMap.ContainsKey(codec) && nativeMap[codec] == outputFormat;
        }

        private string GetFFmpegFormat(string extension)
        {
            // Map file extensions to FFmpeg format names
            return extension switch
            {
                "srt" => "srt",
                "ass" => "ass",
                "ssa" => "ass",
                "vtt" => "webvtt",
                "sub" => "microdvd",
                _ => "srt"
            };
        }

        private void ConvertSubtitleToSrt(string subtitlePath, string subsFolder, string baseFileName, string language)
        {
            try
            {
                string srtOutputPath = Path.Combine(subsFolder, $"{baseFileName}.{language}.srt");

                if (File.Exists(srtOutputPath))
                {
                    _logger.LogInformation("SRT file already exists: {Path}", srtOutputPath);
                    LogToFile($"SRT file already exists: {srtOutputPath}");
                    return;
                }

                if (Path.GetExtension(subtitlePath).ToLower() == ".srt")
                {
                    _logger.LogInformation("Source file is already SRT: {Path}", subtitlePath);
                    LogToFile($"Source file is already SRT: {subtitlePath}");
                    return;
                }

                string convertArgs = $"-i \"{subtitlePath}\" -f srt \"{srtOutputPath}\"";

                var convertInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = convertArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Converting subtitle to SRT: {Input} -> {Output}", subtitlePath, srtOutputPath);
                LogToFile($"FFmpeg conversion command: {_settings.FFmpegPath} {convertArgs}");

                using (var convertProcess = Process.Start(convertInfo))
                {
                    _currentFFmpegProcess = convertProcess; // Track the process

                    // Read output asynchronously to prevent deadlock
                    convertProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            LogToFile(e.Data.Trim(), "FFMPEG");
                    };

                    convertProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            LogToFile(e.Data.Trim(), "FFMPEG");
                    };

                    convertProcess.BeginOutputReadLine();
                    convertProcess.BeginErrorReadLine();

                    convertProcess.WaitForExit();
                    convertProcess.WaitForExit();

                    _currentFFmpegProcess = null;

                    if (convertProcess.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully converted to SRT: {Output}", srtOutputPath);
                        LogToFile($"Successfully converted to SRT: {srtOutputPath}", "SUCCESS");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to convert subtitle to SRT");
                        LogToFile($"Failed to convert subtitle to SRT, exit code: {convertProcess.ExitCode}", "ERROR");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting subtitle to SRT");
                LogToFile($"Error converting subtitle to SRT: {ex.Message}", "ERROR");
            }
        }


        private bool ShouldTranscode(string videoFile)
        {
            // 
            return true;
        }

        private void TranscodeVideo(string inputFile)
        {

            string sourceDirectory = _settings.MonitoredDirectories
                .FirstOrDefault(dir => inputFile.StartsWith(dir, StringComparison.OrdinalIgnoreCase));

            if (sourceDirectory == null)
            {
                _logger.LogWarning("Could not determine source directory for: {File}", inputFile);
                LogToFile($"Could not determine source directory for: {inputFile}", "WARNING");
                return;
            }

            string outputFile;
            if (_settings.PreserveFolderStructure)
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, inputFile);
                outputFile = Path.Combine(_settings.OutputDirectory, relativePath);
            }
            else
            {
                outputFile = Path.Combine(_settings.OutputDirectory, Path.GetFileName(inputFile));
            }

            if (!string.IsNullOrEmpty(_settings.OutputFileExtension))
            {
                outputFile = Path.ChangeExtension(
                    outputFile,
                    _settings.OutputFileExtension.StartsWith(".")
                        ? _settings.OutputFileExtension
                        : "." + _settings.OutputFileExtension
                );
            }

            string outputDir = Path.GetDirectoryName(outputFile);
            Directory.CreateDirectory(outputDir);

            // Track current output file
            _currentOutputFile = outputFile;

            // Extract subtitles BEFORE transcoding
            if (_settings.ExtractSubtitles)
            {
                _logger.LogInformation("Extracting subtitles from: {Input}", inputFile);
                LogToFile($"Extracting subtitles from: {inputFile}");
                ExtractSubtitles(inputFile, outputDir);

                // Check for stop signal after subtitle extraction
                if (ServiceControl.IsStopRequested())
                {
                    LogToFile("Stop requested during subtitle extraction");
                    return;
                }
            }

            DateTime startTime = DateTime.Now;
            _currentTranscodeStartTime = startTime; // Store it as a field so event handlers can access it


            // Set initial progress
            ProgressManager.Save(new TranscodeProgress
            {
                IsTranscoding = true,
                CurrentFile = inputFile,
                OutputFile = outputFile,
                PercentComplete = 0,
                StartTime = startTime,
                Status = "Starting transcode...",
                EstimatedTimeRemaining = TimeSpan.Zero
            });

            string args = $"-i \"{inputFile}\" -o \"{outputFile}\" {_settings.HandBrakeParameters}"; 

            _logger.LogInformation("Transcoding: {Input} -> {Output}", inputFile, outputFile);
            LogToFile($"========================================");
            LogToFile($"Starting transcode: {inputFile}");
            LogToFile($"Output: {outputFile}");
            LogToFile($"HandBrake command: {_settings.HandBrakePath} {args}");
            LogToFile($"========================================");

            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.HandBrakePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            bool success = false;
            bool stoppedByUser = false;
            using (var process = Process.Start(startInfo))
            {
                _currentHandBrakeProcess = process; // Track the process
                // Lower priority AFTER start
                process.PriorityClass = ProcessPriorityClass.BelowNormal;

                // Capture handbrake error output
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogDebug(e.Data);
                        LogToFile(e.Data, "HANDBRAKE-ERR");
                    }
                };

                // Capture handbrake standard output
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogToFile(e.Data, "HANDBRAKE");

                        // Parse HandBrake progress here (moved from ErrorDataReceived)
                        if (e.Data.Contains("Encoding:") && e.Data.Contains("%"))
                        {
                            //LogToFile($"ATTEMPTING TO PARSE: {e.Data}", "DEBUG");

                            try
                            {
                                double percent = 0;
                                double fps = 0;
                                TimeSpan eta = TimeSpan.Zero;

                                // Extract percentage
                                var percentMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"(\d+(?:\.\d+)?)\s*%");
                                if (percentMatch.Success)
                                {
                                    percent = double.Parse(percentMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                                    //LogToFile($"Parsed percent: {percent}", "DEBUG");
                                }

                                // Extract current FPS
                                var fpsMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"\((\d+(?:\.\d+)?)\s*fps");
                                if (fpsMatch.Success)
                                {
                                    fps = double.Parse(fpsMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                                    //LogToFile($"Parsed FPS: {fps}", "DEBUG");
                                }

                                // Extract ETA
                                var etaMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"ETA\s+(\d+)h(\d+)m(\d+)s");
                                if (etaMatch.Success)
                                {
                                    int hours = int.Parse(etaMatch.Groups[1].Value);
                                    int minutes = int.Parse(etaMatch.Groups[2].Value);
                                    int seconds = int.Parse(etaMatch.Groups[3].Value);
                                    eta = new TimeSpan(hours, minutes, seconds);
                                    //LogToFile($"Parsed ETA: {eta}", "DEBUG");
                                }
                                else if (percent > 0 && percent < 100)
                                {
                                    // Fallback: calculate ETA ourselves
                                    TimeSpan elapsed = DateTime.Now - _currentTranscodeStartTime;
                                    double totalEstimatedSeconds = (elapsed.TotalSeconds / percent) * 100;
                                    double remainingSeconds = totalEstimatedSeconds - elapsed.TotalSeconds;
                                    eta = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
                                    //LogToFile($"Calculated ETA: {eta}", "DEBUG");
                                }

                                // Only update if we successfully parsed something
                                if (percent > 0)
                                {
                                    //LogToFile($"SAVING PROGRESS: {percent}%", "DEBUG");

                                    ProgressManager.Save(new TranscodeProgress
                                    {
                                        IsTranscoding = true,
                                        CurrentFile = inputFile,
                                        OutputFile = outputFile,
                                        PercentComplete = percent,
                                        StartTime = _currentTranscodeStartTime,
                                        Status = $"Encoding: {percent:F1}%",
                                        EstimatedTimeRemaining = eta,
                                        CurrentFPS = fps
                                    });

                                    //LogToFile($"Progress saved: {percent}%", "DEBUG");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile($"Error parsing HandBrake progress: {ex.Message}", "ERROR");
                                LogToFile($"Problematic line: {e.Data}", "ERROR");
                            }
                        }
                    }
                };

                LogToFile("Starting async output reading", "DEBUG");
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                LogToFile("Async reading started", "DEBUG");

                // Wait with periodic stop checks instead of blocking
                while (!process.HasExited)
                {
                    if (ServiceControl.IsStopRequested())
                    {
                        LogToFile("Stop requested during transcode - killing HandBrake");
                        _logger.LogInformation("Stop requested - terminating HandBrake process");

                        try
                        {
                            // Kill the process
                            if (!process.HasExited)
                            {
                                process.Kill();
                                process.WaitForExit(5000); // Wait up to 5 seconds for clean exit
                            }
                            stoppedByUser = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Error killing HandBrake process: {Error}", ex.Message);
                            LogToFile($"Error killing HandBrake: {ex.Message}", "ERROR");
                        }
                        break;
                    }

                    System.Threading.Thread.Sleep(500); // Check every 500ms
                }

                // Only wait if not stopped by user
                if (!stoppedByUser && !process.HasExited)
                {
                    process.WaitForExit();
                    process.WaitForExit(); // Second call for async output
                }

                _currentHandBrakeProcess = null;

                if (stoppedByUser)
                {
                    LogToFile("Transcode stopped by user", "WARNING");

                    // Delete incomplete output file
                    if (File.Exists(outputFile))
                    {
                        try
                        {
                            LogToFile($"Deleting incomplete output file: {outputFile}");
                            File.Delete(outputFile);
                            LogToFile("Incomplete file deleted successfully");
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"Failed to delete incomplete file: {ex.Message}", "ERROR");
                        }
                    }
                }
                else if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Successfully transcoded: {Output}", outputFile);
                    LogToFile($"Successfully transcoded: {outputFile}", "SUCCESS");
                    success = true;

                    if (_settings.DeleteOriginalAfterTranscode)
                    {
                        File.Delete(inputFile);
                        _logger.LogInformation("Deleted original: {Input}", inputFile);
                        LogToFile($"Deleted original: {inputFile}");
                    }
                }
                else
                {
                    _logger.LogError("Transcode failed with exit code: {ExitCode}", process.ExitCode);
                    LogToFile($"Transcode failed with exit code: {process.ExitCode}", "ERROR");
                }
            }

            // Record in history
            HistoryManager.RemoveEntry(inputFile); //if you wnt to prevent duplicate entries
            HistoryManager.AddEntry(inputFile, outputFile, success);

            // Clear progress
            ProgressManager.ClearProgress();

            // Clear current output file tracking
            _currentOutputFile = null;

            LogToFile($"========================================");
            LogToFile($"Finished processing: {inputFile}");
            LogToFile($"========================================");
        }


        private void LogToFile(string message, string level = "INFO")
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoTranscoder");

                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                string logPath = Path.Combine(logDir, "service.log");

                // Check file size and rotate if needed (10MB = 10,485,760 bytes)
                const long maxLogSize = 10 * 1024 * 1024; // 10MB

                if (File.Exists(logPath))
                {
                    FileInfo logFile = new FileInfo(logPath);

                    if (logFile.Length > maxLogSize)
                    {
                        RotateLogFile(logPath, logDir);
                    }
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private void RotateLogFile(string currentLogPath, string logDir)
        {
            try
            {
                // Keep last 5 log files
                const int maxLogFiles = 5;

                // Archive current log with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string archivedLogPath = Path.Combine(logDir, $"service_{timestamp}.log");

                // Move current log to archived name
                File.Move(currentLogPath, archivedLogPath);

                _logger.LogInformation("Log file rotated to: {ArchivedLog}", archivedLogPath);

                // Clean up old log files if we have more than maxLogFiles
                var logFiles = Directory.GetFiles(logDir, "service_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (logFiles.Count > maxLogFiles)
                {
                    foreach (var oldLog in logFiles.Skip(maxLogFiles))
                    {
                        try
                        {
                            oldLog.Delete();
                            _logger.LogInformation("Deleted old log file: {OldLog}", oldLog.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to delete old log file {File}: {Error}", oldLog.Name, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rotating log file");
            }
        }
    }
}