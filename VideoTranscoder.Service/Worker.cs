using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using VideoTranscoder.Service;
using VideoTranscoder.Shared;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VideoTranscoder.Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly FileLogger _fileLogger;
        private readonly IHttpClientFactory _httpClientFactory;

        private TranscoderSettings _settings;
        private string _currentOutputFile = null;
        private Process _currentHandBrakeProcess = null;
        private Process _currentFFmpegProcess = null;
        private DateTime _currentTranscodeStartTime;

        public Worker(
            ILogger<Worker> logger,
            IHostApplicationLifetime appLifetime,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _appLifetime = appLifetime;
            _fileLogger = new FileLogger(_logger);
            _httpClientFactory = httpClientFactory;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Video Transcoder Service started");
            _fileLogger.LogToFile("========================================");
            _fileLogger.LogToFile("Video Transcoder Service started");
            _fileLogger.LogToFile("========================================");

            ServiceControl.ClearStopRequest();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (ServiceControl.IsStopRequested())
                    {
                        _logger.LogInformation("Stop requested by user");
                        _fileLogger.LogToFile("Stop requested by user - cleaning up...");
                        CleanupCurrentOperation();
                        ServiceControl.ClearStopRequest();

                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

                        _appLifetime.StopApplication();
                        return;
                    }

                    //Load and validate settings at startup
                    try
                    {
                        _settings = SettingsManager.Load();

                        var errors = _settings.Validate();
                        if (errors.Any())
                        {
                            _logger.LogError("Configuration errors found:");
                            foreach (var error in errors)
                            {
                                _logger.LogError("  - {Error}", error);
                                _fileLogger.LogToFile($"CONFIG ERROR: {error}", "ERROR");
                            }

                            _fileLogger.LogToFile("Service cannot start due to configuration errors", "ERROR");
                            _appLifetime.StopApplication();
                            return;
                        }

                        var warnings = _settings.GetWarnings();
                        if (warnings.Any())
                        {
                            _logger.LogWarning("Configuration warnings:");
                            foreach (var warning in warnings)
                            {
                                _logger.LogWarning("  - {Warning}", warning);
                                _fileLogger.LogToFile(warning, "WARNING");
                            }
                        }

                        _fileLogger.LogToFile("Configuration validated successfully", "INFO");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load configuration");
                        _fileLogger.LogToFile($"Failed to load configuration: {ex.Message}", "ERROR");
                        _appLifetime.StopApplication();
                        return;
                    }

                    _fileLogger.LogToFile("Settings reloaded");

                    if (IsWithinScheduledWindow())
                    {
                        _logger.LogInformation("Within scheduled window - starting scan and transcode process");
                        _fileLogger.LogToFile("Within scheduled window - starting scan");

                        await ProcessVideosAsync(stoppingToken);

                        if (ServiceControl.IsStopRequested())
                        {
                            continue;
                        }

                        _logger.LogInformation("Videos processed. Next check will be in: " + _settings.CheckIntervalMinutes + " minutes.");
                        _fileLogger.LogToFile($"Videos processed. Next check will be in: {_settings.CheckIntervalMinutes} minutes.");

                        await Task.Delay(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), stoppingToken);
                    }
                    else
                    {
                        TimeSpan timeUntilNextWindow = CalculateTimeUntilNextWindow();
                        _logger.LogInformation("Outside scheduled window. Next window starts in {Time}",
                            FormatTimeSpan(timeUntilNextWindow));
                        _fileLogger.LogToFile($"Outside scheduled window. Next window starts in {FormatTimeSpan(timeUntilNextWindow)}");

                        if (ServiceControl.IsStopRequested())
                        {
                            continue;
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
                    _fileLogger.LogToFile($"Error in service execution: {ex.Message}", "ERROR");
                    _fileLogger.LogToFile($"Stack trace: {ex.StackTrace}", "ERROR");

                    if (ServiceControl.IsStopRequested())
                    {
                        continue;
                    }

                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _fileLogger.LogToFile("========================================");
            _fileLogger.LogToFile("Video Transcoder Service stopped");
            _fileLogger.LogToFile("========================================");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service stopping...");
            _fileLogger.LogToFile("Service stopping...");
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
                    _fileLogger.LogToFile("Killing HandBrake process");
                    _currentHandBrakeProcess.Kill();
                    _currentHandBrakeProcess.WaitForExit();
                    _currentHandBrakeProcess = null;
                }

                // Kill FFmpeg process if running
                if (_currentFFmpegProcess != null && !_currentFFmpegProcess.HasExited)
                {
                    _logger.LogInformation("Killing FFmpeg process");
                    _fileLogger.LogToFile("Killing FFmpeg process");
                    _currentFFmpegProcess.Kill();
                    _currentFFmpegProcess.WaitForExit();
                    _currentFFmpegProcess = null;
                }

                // Delete incomplete output file
                if (!string.IsNullOrEmpty(_currentOutputFile) && File.Exists(_currentOutputFile))
                {
                    _logger.LogInformation("Deleting incomplete output file: {File}", _currentOutputFile);
                    _fileLogger.LogToFile($"Deleting incomplete output file: {_currentOutputFile}");

                    try
                    {
                        File.Delete(_currentOutputFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to delete output file: {Error}", ex.Message);
                        _fileLogger.LogToFile($"Failed to delete output file: {ex.Message}", "WARNING");
                    }
                }

                // Clear progress
                ProgressManager.ClearProgress();

                _currentOutputFile = null;

                _fileLogger.LogToFile("Cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
                _fileLogger.LogToFile($"Error during cleanup: {ex.Message}", "ERROR");
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

        private async Task ProcessVideosAsync(CancellationToken cancellationToken)
        {
            foreach (var dir in _settings.MonitoredDirectories)
            {
                // Check for stop signal
                if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                {
                    _fileLogger.LogToFile("Stop requested during processing videos");
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
                    if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                    {
                        _fileLogger.LogToFile("Stop requested during processing video files");
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
                            await TranscodeVideoAsync(file, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing {File}", file);
                    }
                }
            }
        }

        private void CopyExternalSubtitles(string inputFile, string outputDirectory)
        {
            if (!_settings.CopyExternalSubtitles)
                return;

            try
            {
                string inputDir = Path.GetDirectoryName(inputFile);
                string inputFileName = Path.GetFileNameWithoutExtension(inputFile);

                // Create subs folder in output directory
                string outputSubsFolder = Path.Combine(outputDirectory, "subs");
                Directory.CreateDirectory(outputSubsFolder);

                // Common subtitle extensions
                //string[] subtitleExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup" };
                List<string> subtitleExtensions = _settings.GetSubtitleFormatsList(); //.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                List<string> targetLanguages = _settings.GetSubtitleLanguagesList();
                bool copyAllLanguages = targetLanguages == null || targetLanguages.Count == 0;

                // Build language mapping if we need to filter by language
                Dictionary<string, string> languageMapping = null;
                if (!copyAllLanguages)
                {
                    languageMapping = _settings.SubtitleLanguagesDictionary;
                }

                // Track copied subtitles
                int copiedCount = 0;



                // 1. Check for subtitles in the same directory as the video file
                _fileLogger.LogToFile($"Searching for external subtitles for: {inputFileName}", "INFO");
                copiedCount += CopySubtitlesFromDirectory(inputDir, inputFileName, subtitleExtensions,
                    outputSubsFolder, copyAllLanguages, languageMapping, "same directory");

                // 2. Check for "subtitles" subfolder in the input directory
                string inputSubtitlesFolder = Path.Combine(inputDir, "subtitles");
                if (Directory.Exists(inputSubtitlesFolder))
                {
                    _fileLogger.LogToFile($"Found 'subtitles' directory: {inputSubtitlesFolder}", "INFO");
                    copiedCount += CopySubtitlesFromDirectory(inputSubtitlesFolder, inputFileName, subtitleExtensions,
                        outputSubsFolder, copyAllLanguages, languageMapping, "subtitles folder");
                }

                // 3. Check for "subs" subfolder in the input directory
                string inputSubsFolder = Path.Combine(inputDir, "subs");
                if (Directory.Exists(inputSubsFolder))
                {
                    _fileLogger.LogToFile($"Found 'subs' directory: {inputSubsFolder}", "INFO");
                    copiedCount += CopySubtitlesFromDirectory(inputSubsFolder, inputFileName, subtitleExtensions,
                        outputSubsFolder, copyAllLanguages, languageMapping, "subs folder");
                }

                if (copiedCount > 0)
                {
                    _logger.LogInformation("Copied {Count} external subtitle file(s) for: {File}", copiedCount, inputFile);
                    _fileLogger.LogToFile($"Total external subtitles copied: {copiedCount}", "INFO");
                }
                else
                {
                    _fileLogger.LogToFile($"No external subtitles found for: {inputFileName}", "INFO");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying external subtitles from: {File}", inputFile);
                _fileLogger.LogToFile($"Error copying external subtitles: {ex.Message}", "ERROR");
            }

        }

        private int CopySubtitlesFromDirectory(string sourceDir, string baseFileName, List<string> subtitleExtensions,
            string outputSubsFolder, bool copyAllLanguages, Dictionary<string, string> languageMapping, string sourceName)
        {
            int copiedCount = 0;

            foreach (var ext in subtitleExtensions)
            {
                var matchingFiles = Directory.GetFiles(sourceDir, $"{baseFileName}*{ext}", SearchOption.TopDirectoryOnly);

                foreach (var subtitleFile in matchingFiles)
                {
                    string subtitleFileName = Path.GetFileName(subtitleFile);

                    // Check if we should copy this subtitle based on language filter
                    if (!copyAllLanguages)
                    {
                        string languageCode = LanguageCodeUtils.ExtractLanguageCode(
                            Path.GetFileNameWithoutExtension(subtitleFile),
                            baseFileName
                        );

                        //we copy subtitles without language code and those that match language code
                        if (!string.IsNullOrEmpty(languageCode) && !LanguageCodeUtils.IsLanguageMatch(languageCode, languageMapping)) //we include subtitles without language code. else put string.IsNullOrEmpty(languageCode) || 
                        {
                            _fileLogger.LogToFile($"Skipping subtitle (language not in filter): {subtitleFileName}", "INFO");
                            continue;
                        }
                    }

                    string destinationPath = Path.Combine(outputSubsFolder, subtitleFileName);

                    bool shouldCopy = false;
                    if (_settings.OverwriteExistingSubtitles)
                    {
                        shouldCopy = true;
                    }
                    else
                    {
                        if(File.Exists(destinationPath))
                        {
                            _fileLogger.LogToFile($"Subtitle already exists and overwrite is disabled.", "INFO");
                        }
                        else
                        {
                            shouldCopy = true;
                        }
                    }

                    if (shouldCopy) { 
                        try
                        {
                            File.Copy(subtitleFile, destinationPath, overwrite: true);
                            _fileLogger.LogToFile($"Copied external subtitle from {sourceName}: {subtitleFileName}", "SUCCESS");
                            copiedCount++;
                        }
                        catch (Exception ex)
                        {
                            _fileLogger.LogToFile($"Failed to copy {subtitleFileName}: {ex.Message}", "WARNING");
                        }
                    }
                }
            }

            return copiedCount;
        }

        private void ExtractSubtitles(string inputFile, string outputDirectory, DateTime startTime)
        {
            if (!_settings.ExtractSubtitles)
                return;

            if (!File.Exists(_settings.FFmpegPath))
            {
                _logger.LogWarning("FFmpeg not found at: {Path}", _settings.FFmpegPath);
                return;
            }

            _logger.LogInformation("Checking included subtitles for: {Input}", inputFile);
            _fileLogger.LogToFile($"Checking included subtitles for: {inputFile}");

            try
            {
                // Report that we're starting subtitle extraction
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = true,
                    CurrentFile = inputFile,
                    StartTime = startTime,
                    Status = "Analyzing subtitles...",
                    SubtitleExtractionStatus = "Probing video file for subtitle streams"
                });

                // First, probe the file to see what subtitle streams exist
                var probeInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = $"-y -i \"{inputFile}\"",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string probeOutput = "";
                using (var probeProcess = Process.Start(probeInfo))
                {
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
                    probeProcess.WaitForExit();

                    probeOutput = errorBuilder.ToString();
                }

                _fileLogger.LogToFile($"Probe completed, analyzing subtitle streams");

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
                                var langMatch = System.Text.RegularExpressions.Regex.Match(line, @"\(([a-z]{2,3})\)");
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
                    _logger.LogInformation("No included subtitles found in video file: {File}", inputFile);
                    _fileLogger.LogToFile($"No included subtitles found in video file: {inputFile}");

                    ProgressManager.Save(new TranscodeProgress
                    {
                        IsTranscoding = true,
                        IsExtractingSubtitles = false,
                        CurrentFile = inputFile,
                        StartTime = _currentTranscodeStartTime,
                        Status = "No subtitles found",
                        SubtitleExtractionStatus = "No subtitle streams detected"
                    });

                    return;
                }

                // Create subs folder
                string subsFolder = Path.Combine(outputDirectory, "subs");
                Directory.CreateDirectory(subsFolder);

                int Count = subtitleStreams.Count;
                _logger.LogInformation("Found {Count} subtitle stream(s) in: {File}", Count, inputFile);
                _fileLogger.LogToFile($"Found {Count} subtitle stream(s) in: {inputFile}");

                // Update progress with total count
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = true,
                    CurrentFile = inputFile,
                    StartTime = _currentTranscodeStartTime,
                    Status = "Extracting subtitles...",
                    SubtitleExtractionStatus = $"Found {subtitleStreams.Count} subtitle stream(s)",
                    TotalSubtitleStreams = subtitleStreams.Count,
                    ProcessedSubtitleStreams = 0
                });

                // Extract each subtitle stream
                var allowedFormats = _settings.GetSubtitleFormatsList();
                string baseFileName = Path.GetFileNameWithoutExtension(inputFile);
                var extractedFiles = new Dictionary<string, List<(string path, string language, string codec)>>();

                // First pass: count how many subtitles we have per language+format combination
                var subtitleCounts = new Dictionary<string, int>();
                foreach (var (streamIndex, language, codec) in subtitleStreams)
                {
                    string outputFormat = DetermineOutputFormat(codec, allowedFormats);
                    string key = $"{language}.{outputFormat}";
                    subtitleCounts[key] = subtitleCounts.ContainsKey(key) ? subtitleCounts[key] + 1 : 1;
                }

                // Second pass: extract subtitles with appropriate naming
                var streamIndexCounters = new Dictionary<string, int>(); // Track how many we've extracted per lang+format


                for (int i = 0; i < subtitleStreams.Count; i++)
                {
                    var (streamIndex, language, codec) = subtitleStreams[i];

                    // Update progress
                    ProgressManager.Save(new TranscodeProgress
                    {
                        IsTranscoding = true,
                        IsExtractingSubtitles = true,
                        CurrentFile = inputFile,
                        StartTime = _currentTranscodeStartTime,
                        Status = "Extracting subtitles...",
                        SubtitleExtractionStatus = $"Extracting subtitle {i + 1}/{subtitleStreams.Count} ({language}, {codec})",
                        TotalSubtitleStreams = subtitleStreams.Count,
                        ProcessedSubtitleStreams = i
                    });

                    // Determine output format based on user preferences
                    string outputFormat = DetermineOutputFormat(codec, allowedFormats);
                    string key = $"{language}.{outputFormat}";

                    // Check if we need to include stream index in filename
                    bool needsStreamIndex = subtitleCounts[key] > 1;

                    // Track current count for this language+format
                    if (!streamIndexCounters.ContainsKey(key))
                    {
                        streamIndexCounters[key] = 0;
                    }
                    int currentCount = streamIndexCounters[key];
                    streamIndexCounters[key]++;

                    // Build output filename
                    string subtitleFileName;
                    if (needsStreamIndex && currentCount > 0)
                    {
                        // First one has no index, subsequent ones get index: movie.en.srt, movie.1.en.srt, movie.2.en.srt
                        subtitleFileName = $"{baseFileName}.{currentCount}.{language}.{outputFormat}";
                    }
                    else
                    {
                        subtitleFileName = $"{baseFileName}.{language}.{outputFormat}";
                    }

                    string subtitleOutputPath = Path.Combine(subsFolder, subtitleFileName);

                    // Extract subtitle
                    bool extracted = ExtractSingleSubtitle(inputFile, streamIndex, subtitleOutputPath, codec, outputFormat);

                    // If extraction succeeded OR file already exists, add to our tracking
                    if (extracted || File.Exists(subtitleOutputPath))
                    {
                        if (!extractedFiles.ContainsKey(language))
                        {
                            extractedFiles[language] = new List<(string, string, string)>();
                        }
                        extractedFiles[language].Add((subtitleOutputPath, language, codec));

                        if (!extracted)
                        {
                            _logger.LogInformation("Subtitle already exists, skipped: {Output}", subtitleOutputPath);
                        }
                    }
                }

                int totalExtracted = extractedFiles.Sum(kvp => kvp.Value.Count);

                // Update progress after extraction
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = true,
                    CurrentFile = inputFile,
                    StartTime = _currentTranscodeStartTime,
                    Status = "Extracting subtitles...",
                    SubtitleExtractionStatus = $"Extracted {totalExtracted}/{subtitleStreams.Count} subtitle streams",
                    TotalSubtitleStreams = subtitleStreams.Count,
                    ProcessedSubtitleStreams = subtitleStreams.Count
                });

                // Convert subtitles to SRT if missing for each requested language
                if (_settings.ConvertToSrtIfMissing)
                {
                    List<string> targetLanguages = _settings.GetSubtitleLanguagesList();

                    var languageMapping = _settings.SubtitleLanguagesDictionary;

                    ProgressManager.Save(new TranscodeProgress
                    {
                        IsTranscoding = true,
                        IsExtractingSubtitles = true,
                        CurrentFile = inputFile,
                        StartTime = _currentTranscodeStartTime,
                        Status = "Converting subtitles...",
                        SubtitleExtractionStatus = "Checking for missing SRT subtitles...",
                        TotalSubtitleStreams = subtitleStreams.Count,
                        ProcessedSubtitleStreams = subtitleStreams.Count
                    });

                    foreach (var targetLang in targetLanguages)
                    {
                        // Check if SRT already exists for this language
                        bool srtExists = CheckIfSrtExists(subsFolder, baseFileName, targetLang, languageMapping);

                        if (srtExists)
                        {
                            _logger.LogInformation("SRT subtitle already exists for language: {Language}", targetLang);
                            _fileLogger.LogToFile($"SRT subtitle already exists for language: {targetLang}");
                            continue;
                        }

                        // Find a subtitle for this language that we can convert
                        var subtitleToConvert = FindSubtitleForLanguage(extractedFiles, targetLang, languageMapping);

                        if (subtitleToConvert != null)
                        {
                            _logger.LogInformation("Starting to convert subtitle to SRT for language: {Language}", targetLang);
                            _fileLogger.LogToFile($"Starting to convert subtitle to SRT for language: {targetLang}");

                            ConvertSubtitleToSrt(subtitleToConvert.Value.path, subsFolder, baseFileName, subtitleToConvert.Value.language);
                        }
                        else
                        {
                            _logger.LogInformation("No subtitle found in video for language: {Language}", targetLang);
                            _fileLogger.LogToFile($"No subtitle found in video for language: {targetLang}");
                        }
                    }
                }

                // Final progress update
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = false,
                    CurrentFile = inputFile,
                    StartTime = _currentTranscodeStartTime,
                    Status = "Subtitle extraction complete",
                    SubtitleExtractionStatus = $"Completed: {totalExtracted} subtitle(s) extracted",
                    TotalSubtitleStreams = subtitleStreams.Count,
                    ProcessedSubtitleStreams = subtitleStreams.Count
                });

                _logger.LogInformation("Subtitle extraction complete for: {File}", inputFile);
                _fileLogger.LogToFile($"Subtitle extraction complete for: {inputFile}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting subtitles from: {File}", inputFile);
                _fileLogger.LogToFile($"Error extracting subtitles from: {inputFile} ERROR msg: {ex.Message}", "ERROR");

                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    IsExtractingSubtitles = false,
                    CurrentFile = inputFile,
                    StartTime = _currentTranscodeStartTime,
                    Status = "Subtitle extraction failed",
                    SubtitleExtractionStatus = $"Error: {ex.Message}"
                });
            }
        }

        private bool CheckIfSrtExists(string subsFolder, string baseFileName, string targetLang, Dictionary<string, string> languageMapping)
        {
            string twoLetterCode = targetLang;
            string threeLetterCode = languageMapping.ContainsKey(targetLang) ? languageMapping[targetLang] : targetLang;

            // Check for files like: movie.en.srt, movie.eng.srt, movie.en.0.srt, etc.
            var srtFiles = Directory.GetFiles(subsFolder, $"{baseFileName}*.srt", SearchOption.TopDirectoryOnly);

            foreach (var srtFile in srtFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(srtFile);
                string remainder = fileName.Substring(baseFileName.Length).TrimStart('.');

                if (string.IsNullOrEmpty(remainder))
                    continue;

                var parts = remainder.Split('.');

                // Check if any part matches our language codes
                foreach (var part in parts)
                {
                    if (part.Equals(twoLetterCode, StringComparison.OrdinalIgnoreCase) ||
                        part.Equals(threeLetterCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private (string path, string language, string codec)? FindSubtitleForLanguage(
            Dictionary<string, List<(string path, string language, string codec)>> extractedFiles,
            string targetLang,
            Dictionary<string, string> languageMapping)
        {
            string twoLetterCode = targetLang;
            string threeLetterCode = languageMapping.ContainsKey(targetLang) ? languageMapping[targetLang] : targetLang;

            // First, try exact match with 2-letter code
            if (extractedFiles.ContainsKey(twoLetterCode))
            {
                var candidates = extractedFiles[twoLetterCode];
                // Prefer non-SRT formats (since we want to convert TO srt)
                var nonSrt = candidates.FirstOrDefault(c => !c.path.EndsWith(".srt", StringComparison.OrdinalIgnoreCase));
                return nonSrt.path != null ? nonSrt : candidates.FirstOrDefault();
            }

            // Try 3-letter code
            if (extractedFiles.ContainsKey(threeLetterCode))
            {
                var candidates = extractedFiles[threeLetterCode];
                var nonSrt = candidates.FirstOrDefault(c => !c.path.EndsWith(".srt", StringComparison.OrdinalIgnoreCase));
                return nonSrt.path != null ? nonSrt : candidates.FirstOrDefault();
            }

            // Try case-insensitive search
            foreach (var kvp in extractedFiles)
            {
                if (kvp.Key.Equals(twoLetterCode, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals(threeLetterCode, StringComparison.OrdinalIgnoreCase))
                {
                    var candidates = kvp.Value;
                    var nonSrt = candidates.FirstOrDefault(c => !c.path.EndsWith(".srt", StringComparison.OrdinalIgnoreCase));
                    return nonSrt.path != null ? nonSrt : candidates.FirstOrDefault();
                }
            }

            return null;
        }

        private string DetermineOutputFormat(string codec, List<string> allowedFormats)
        {
            var codecMap = new Dictionary<string, string>
    {
        { "subrip", "srt" },
        { "srt", "srt" },
        { "ass", "ass" },
        { "ssa", "ass" },
        { "webvtt", "vtt" },
        { "vtt", "vtt" },
        { "mov_text", "srt" },
        { "hdmv_pgs_subtitle", "sup" },
        { "dvd_subtitle", "sub" }
    };

            string defaultFormat = codecMap.ContainsKey(codec) ? codecMap[codec] : "srt";

            if (allowedFormats.Count == 0)
                return defaultFormat;

            if (allowedFormats.Contains(defaultFormat))
                return defaultFormat;

            return allowedFormats[0];
        }

        private bool ExtractSingleSubtitle(string inputFile, int streamIndex, string outputPath, string codec, string outputFormat)
        {
            try
            {
                bool needsConversion = !IsNativeFormat(codec, outputFormat);
                string overwriteIndicator = _settings.OverwriteExistingSubtitles ? "-y" : "-n";

                // Check if file exists before extraction
                bool fileExists = File.Exists(outputPath);
                if (fileExists && !_settings.OverwriteExistingSubtitles)
                {
                    _logger.LogInformation("Subtitle file already exists, skipped: {Output}", outputPath);
                    _fileLogger.LogToFile($"Subtitle file already exists, skipped: {outputPath}", "SKIPPED");
                    return false;
                }

                //DateTime? originalModifiedTime = fileExistedBefore ? File.GetLastWriteTimeUtc(outputPath) : null;

                string extractArgs;
                if (needsConversion)
                {
                    extractArgs = $"{overwriteIndicator} -i \"{inputFile}\" -map 0:{streamIndex} -f {GetFFmpegFormat(outputFormat)} \"{outputPath}\"";
                }
                else
                {
                    extractArgs = $"{overwriteIndicator} -i \"{inputFile}\" -map 0:{streamIndex} -c copy \"{outputPath}\"";
                }

                var extractInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = extractArgs,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Extracting single subtitle stream {Index} to: {Output}", streamIndex, outputPath);
                _fileLogger.LogToFile($"FFmpeg command: {_settings.FFmpegPath} {extractArgs}");

                using (var extractProcess = Process.Start(extractInfo))
                {
                    _currentFFmpegProcess = extractProcess;

                    extractProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            // Don't log everything to file
                        }
                    };

                    extractProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            _fileLogger.LogToFile(e.Data.Trim(), "FFMPEG");
                    };

                    extractProcess.BeginOutputReadLine();
                    extractProcess.BeginErrorReadLine();

                    extractProcess.WaitForExit();
                    extractProcess.WaitForExit();

                    _currentFFmpegProcess = null;

                    if (extractProcess.ExitCode == 0)
                    {

                        if (new FileInfo(outputPath).Length < 1024)
                        {
                            _logger.LogInformation("Probably something went worng. Extracted subtitle file is less than 1KB. It will be deleted. {Path}", outputPath);
                            _fileLogger.LogToFile($"Probably something went worng. Extracted subtitle file is less than 1KB. It will be deleted. {outputPath}");
                            File.Delete(outputPath);
                            return false;
                        }


                        _logger.LogInformation("Successfully extracted subtitle: {Output}", outputPath);
                        _fileLogger.LogToFile($"Successfully extracted subtitle: {outputPath}", "SUCCESS");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to extract subtitle stream {Index}", streamIndex);
                        _fileLogger.LogToFile($"Failed to extract subtitle stream {streamIndex}, exit code: {extractProcess.ExitCode}", "ERROR");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting subtitle stream {Index}", streamIndex);
                _fileLogger.LogToFile($"Error extracting subtitle stream {streamIndex}: {ex.Message}", "ERROR");
                return false;
            }
        }

        private bool IsNativeFormat(string codec, string outputFormat)
        {
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
                    _fileLogger.LogToFile($"SRT file already exists: {srtOutputPath}");
                    return;
                }

                if (Path.GetExtension(subtitlePath).ToLower() == ".srt")
                {
                    _logger.LogInformation("Source file is already SRT: {Path}", subtitlePath);
                    _fileLogger.LogToFile($"Source file is already SRT: {subtitlePath}");
                    return;
                }

                string convertArgs = $"-n -i \"{subtitlePath}\" -f srt \"{srtOutputPath}\"";

                var convertInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = convertArgs,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation("Starting to convert subtitle to SRT: {Input} -> {Output}", subtitlePath, srtOutputPath);
                _fileLogger.LogToFile($"FFmpeg conversion command: {_settings.FFmpegPath} {convertArgs}");

                using (var convertProcess = Process.Start(convertInfo))
                {
                    _currentFFmpegProcess = convertProcess;

                    convertProcess.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            _fileLogger.LogToFile(e.Data.Trim(), "FFMPEG");
                    };

                    convertProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                            _fileLogger.LogToFile(e.Data.Trim(), "FFMPEG");
                    };

                    convertProcess.BeginOutputReadLine();
                    convertProcess.BeginErrorReadLine();

                    convertProcess.WaitForExit();
                    convertProcess.WaitForExit();

                    _currentFFmpegProcess = null;


                    if (convertProcess.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully converted to SRT: {Output}", srtOutputPath);
                        _fileLogger.LogToFile($"Successfully converted to SRT: {srtOutputPath}", "SUCCESS");

                        if (File.Exists(srtOutputPath))
                        {
                            _logger.LogInformation("Converted SRT file was not created {Path}", srtOutputPath);
                            _fileLogger.LogToFile($"Converted SRT file was not created: {srtOutputPath}");
                            return;
                        }else if (new FileInfo(srtOutputPath).Length < 1024)
                        {
                            _logger.LogInformation("Probably something went worng. Converted SRT file is less than 1KB. It will be deleted. {Path}", srtOutputPath);
                            _fileLogger.LogToFile($"Probably something went worng. Converted SRT file is less than 1KB. It will be deleted. {srtOutputPath}");
                            File.Delete(srtOutputPath);
                            return;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to convert subtitle to SRT");
                        _fileLogger.LogToFile($"Failed to convert subtitle to SRT, exit code: {convertProcess.ExitCode}", "ERROR");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting subtitle to SRT");
                _fileLogger.LogToFile($"Error converting subtitle to SRT: {ex.Message}", "ERROR");
            }
        }


        private bool ShouldTranscode(string videoFile)
        {
            return true;
        }

        private async Task DownloadMissingSubtitlesAsync(string inputFile, string outputFile, CancellationToken cancellationToken)
        {
            List<string> validSubLanguages = _settings.GetSubtitleLanguagesList();
            List<string> missingSubtitleLanguages = SubtitleChecker.FindMissingSubtitles(outputFile, validSubLanguages);

            if (!missingSubtitleLanguages.Any())
            {
                _logger.LogInformation("No missing subtitles for: {Output}", outputFile);
                _fileLogger.LogToFile($"No missing subtitles for {outputFile}");
                return;
            }
            else
            {
                _logger.LogInformation("Missing subtitles detected for: {Output}. Languages: {Languages}",
                    outputFile, string.Join(", ", missingSubtitleLanguages));
                _fileLogger.LogToFile($"Missing subtitles for {outputFile}: {string.Join(", ", missingSubtitleLanguages)}");
            }

            OpenSubtitlesClient client = null;
            try
            {
                // Validate settings before creating client
                if (string.IsNullOrWhiteSpace(_settings.OpenSubtitlesApiKey) ||
                    string.IsNullOrWhiteSpace(_settings.OpenSubtitlesAppName))
                {
                    _logger.LogWarning("OpenSubtitles credentials not configured. Skipping subtitle download.");
                    _fileLogger.LogToFile("OpenSubtitles credentials not configured", "WARNING");
                    return;
                }

                //var handler = new LoggingHandler(_fileLogger) { InnerHandler = new HttpClientHandler() };

                var httpClient = _httpClientFactory.CreateClient();
                //var httpClient = new HttpClient(handler);
                var fileHttpClient = _httpClientFactory.CreateClient();
                //var fileHttpClient = new HttpClient(handler);

                client = await OpenSubtitlesClient.CreateAsync(
                    httpClient,
                    fileHttpClient,
                    _settings.OpenSubtitlesAppName,
                    _settings.OpenSubtitlesApiKey,
                    _settings.OpenSubtitlesUsername,
                    _settings.OpenSubtitlesPassword,
                    _settings.FixCorruptedSubtitles,
                    _fileLogger
                );

                foreach (string missingLang in missingSubtitleLanguages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        //Opensubtitles has 5 requests per 1 second limit. Add delay to avoid hitting rate limit.
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                        await DownloadSubtitleForLanguageAsync(client, inputFile, outputFile, missingLang, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _fileLogger.LogToFile("Subtitle download cancelled");
                        throw; // Re-throw cancellation
                    }
                    catch (Exception ex)
                    {
                        // Log but continue with other languages
                        _logger.LogError(ex, "Error downloading subtitle for language: {Language}", missingLang);
                        _fileLogger.LogToFile($"Error downloading subtitle for language {missingLang}: {ex.Message}", "ERROR");
                        // Continue with next language instead of failing completely
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Subtitle download was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenSubtitles client");
                _fileLogger.LogToFile($"Failed to initialize OpenSubtitles client: {ex.Message}", "ERROR");
                // Don't throw - allow transcode to continue without subtitles
            }
            finally
            {
                client?.Dispose();
            }
        }

        private async Task DownloadSubtitleForLanguageAsync(OpenSubtitlesClient client, string inputFile, string outputFile, string language, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Searching OpenSubtitles for {Language} subtitle", language);
            _fileLogger.LogToFile($"Searching OpenSubtitles for {language} subtitle");

            var subtitles = await client.SearchSubtitlesByHashAsync(inputFile, language: language);

            if (!subtitles.Any())
            {
                _logger.LogInformation("No subtitles found by hash for language: {Language}", language);
                _fileLogger.LogToFile($"No subtitles found by hash for language: {language}");

                // Fallback: Try searching by movie name
                /*
                var movieName = Path.GetFileNameWithoutExtension(inputFile);
                _logger.LogInformation("Searching by name: {MovieName}", movieName);

                subtitles = await client.SearchSubtitlesAsync(movieName: movieName, language: language);
                */
            }

            if (!subtitles.Any())
            {
                _logger.LogInformation("No subtitles found on OpenSubtitles for {Language}", language);
                _fileLogger.LogToFile($"No subtitles found on OpenSubtitles for {language}");
                return;
            }

            _logger.LogInformation("Found {Count} matching subtitles", subtitles.Count);
            _fileLogger.LogToFile($"Found {subtitles.Count} matching subtitles");

            var bestSubtitle = subtitles
                .OrderByDescending(s => s.FromTrusted)
                .ThenByDescending(s => s.Rating)
                .ThenByDescending(s => s.DownloadCount)
                .First();

            if (!bestSubtitle.FileId.HasValue)
            {
                _logger.LogWarning("Best subtitle has no file ID");
                _fileLogger.LogToFile("Best subtitle has no file ID", "WARNING");
                return;
            }

            var outputSubsFolder = Path.Combine(Path.GetDirectoryName(outputFile), "subs");
            Directory.CreateDirectory(outputSubsFolder);

            var subtitlePath = Path.Combine(
                outputSubsFolder,
                $"{Path.GetFileNameWithoutExtension(outputFile)}.{language}.srt"
            );

            await client.DownloadSubtitleAsync(bestSubtitle.FileId.Value, subtitlePath, language, 1);

            _logger.LogInformation("Downloaded subtitle: {Path}", subtitlePath);
            _fileLogger.LogToFile($"Downloaded subtitle: {subtitlePath}");
        }

        private async Task TranscodeVideoAsync(string inputFile, CancellationToken cancellationToken)
        {
            string sourceDirectory = _settings.MonitoredDirectories
                .FirstOrDefault(dir => inputFile.StartsWith(dir, StringComparison.OrdinalIgnoreCase));

            if (sourceDirectory == null)
            {
                _logger.LogWarning("Could not determine source directory for: {File}", inputFile);
                _fileLogger.LogToFile($"Could not determine source directory for: {inputFile}", "WARNING");
                return;
            }

            string outputFile;
            if (_settings.PreserveFolderStructure)
            {
                string rootFolderName = new DirectoryInfo(sourceDirectory).Name;
                string relativePath = Path.GetRelativePath(sourceDirectory, inputFile);

                outputFile = Path.Combine(
                    _settings.OutputDirectory,
                    rootFolderName,
                    relativePath
                );
            }
            else
            {
                outputFile = Path.Combine(_settings.OutputDirectory, Path.GetFileName(inputFile));
            }

            if (!string.IsNullOrEmpty(_settings.OutputFileExtension) && _settings.EnableTranscoding)
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

            DateTime startTime = DateTime.Now;
            _currentTranscodeStartTime = startTime;

            // Extract subtitles
            if (_settings.ExtractSubtitles)
            {
                _logger.LogInformation("Starting extraction of subtitles from: {Input}", inputFile);
                _fileLogger.LogToFile($"Starting extraction of subtitles from: {inputFile}");
                ExtractSubtitles(inputFile, outputDir, startTime);

                // Check for stop signal after subtitle extraction
                if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                {
                    _fileLogger.LogToFile("Stop requested during subtitle extraction");
                    return;
                }
            }

            // Copy external subtitles
            if (_settings.CopyExternalSubtitles)
            {
                _logger.LogInformation("External subtitles check for: {Input}", inputFile);
                _fileLogger.LogToFile($"External subtitles check for: {inputFile}");
                CopyExternalSubtitles(inputFile, outputDir);

                // Check for stop signal after subtitle copy
                if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                {
                    _fileLogger.LogToFile("Stop requested during subtitle copy");
                    return;
                }
            }

            if (_settings.DownloadSubtitles)
            {
                _logger.LogInformation("Preparing to download subtitles for: {Input}", inputFile);
                try {
                    await DownloadMissingSubtitlesAsync(inputFile, outputFile, cancellationToken);
                }
                catch (Exception ex)
                {
                    _fileLogger.LogToFile($"Error downloading subtitles: {ex.Message}", "ERROR");
                }

                // Check for stop signal after subtitle download
                if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                {
                    _fileLogger.LogToFile("Stop requested during subtitle download");
                    return;
                }
            }

            bool success = false;
            bool canCreateOutputFile = false;

            _logger.LogInformation("Checking output video file.");
            _fileLogger.LogToFile($"========================================");
            _fileLogger.LogToFile($"Checking output video file: {outputFile}");
            _fileLogger.LogToFile($"========================================");

            if (File.Exists(outputFile))
            {
                _logger.LogInformation("Output file already exists");
                _fileLogger.LogToFile($"Output file already exists: {outputFile}");

                if (_settings.OverwriteExistingVideos) {
                    canCreateOutputFile = true;
                    _logger.LogInformation("Output file wiil be overwritten");
                    _fileLogger.LogToFile("Output file wiil be overwritten");
                }
                else
                {
                    canCreateOutputFile = false;
                    _logger.LogInformation("Skipping video creation.");
                    _fileLogger.LogToFile("Skipping video creation.");
                }
            }
            else
            {
                canCreateOutputFile = true;
                _logger.LogInformation("Output file does not exist. Video can be created.");
                _fileLogger.LogToFile("Output file does not exist. Video can be created.");
            }

            ////////////////////////////////////////////////
            ///// Convert using FFmpeg if enabled
            if (canCreateOutputFile && _settings.ConvertUsingFFmpeg)
            {
                _logger.LogInformation("FFmpeg conversion is enabled, starting conversion");
                _fileLogger.LogToFile("FFmpeg conversion is enabled, starting conversion");

                bool ffmpegSuccess = await ConvertUsingFFmpegAsync(inputFile, outputFile, cancellationToken);

                if (ffmpegSuccess)
                {
                    success = true;

                    if (_settings.DeleteOriginalAfterTranscode)
                    {
                        File.Delete(inputFile);
                        _logger.LogInformation("Deleted original: {Input}", inputFile);
                        _fileLogger.LogToFile($"Deleted original: {inputFile}");
                    }
                }
                else
                {
                    success = false; //TODO Mark a success if not needed
                    _logger.LogWarning("FFmpeg conversion failed or not needed");
                    _fileLogger.LogToFile("FFmpeg conversion failed or not needed", "WARNING");
                }

                HistoryManager.RemoveEntry(inputFile);
                HistoryManager.AddEntry(inputFile, outputFile, success);
                ProgressManager.ClearProgress();
                _currentOutputFile = null;

                _fileLogger.LogToFile($"========================================");
                _fileLogger.LogToFile($"Finished processing with FFmpeg: {inputFile}");
                _fileLogger.LogToFile($"========================================");

                // Check for stop signal after FFmpeg conversion
                if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                {
                    _fileLogger.LogToFile("Stop requested during FFmpeg conversion");
                    return;
                }

                return;

            }
            else



            //////////////////////////////////////////////
            ///

            if (canCreateOutputFile && _settings.EnableTranscoding)
            {
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

                _logger.LogInformation("Starting to transcode: {Input} -> {Output}", inputFile, outputFile);
                _fileLogger.LogToFile($"========================================");
                _fileLogger.LogToFile($"Starting transcode: {inputFile}");
                _fileLogger.LogToFile($"Output: {outputFile}");
                _fileLogger.LogToFile($"HandBrake command: {_settings.HandBrakePath} {args}");
                _fileLogger.LogToFile($"========================================");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _settings.HandBrakePath,
                    Arguments = args,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                bool stoppedByUser = false;
                using (var process = Process.Start(startInfo))
                {
                    _currentHandBrakeProcess = process;
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            _logger.LogDebug(e.Data);
                            _fileLogger.LogToFile(e.Data, "HANDBRAKE-ERR");
                        }
                    };

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            _fileLogger.LogToFile(e.Data, "HANDBRAKE");

                            if (e.Data.Contains("Encoding:") && e.Data.Contains("%"))
                            {
                                try
                                {
                                    double percent = 0;
                                    double fps = 0;
                                    TimeSpan eta = TimeSpan.Zero;

                                    var percentMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"(\d+(?:\.\d+)?)\s*%");
                                    if (percentMatch.Success)
                                    {
                                        percent = double.Parse(percentMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                                    }

                                    var fpsMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"\((\d+(?:\.\d+)?)\s*fps");
                                    if (fpsMatch.Success)
                                    {
                                        fps = double.Parse(fpsMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                                    }

                                    var etaMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"ETA\s+(\d+)h(\d+)m(\d+)s");
                                    if (etaMatch.Success)
                                    {
                                        int hours = int.Parse(etaMatch.Groups[1].Value);
                                        int minutes = int.Parse(etaMatch.Groups[2].Value);
                                        int seconds = int.Parse(etaMatch.Groups[3].Value);
                                        eta = new TimeSpan(hours, minutes, seconds);
                                    }
                                    else if (percent > 0 && percent < 100)
                                    {
                                        TimeSpan elapsed = DateTime.Now - _currentTranscodeStartTime;
                                        double totalEstimatedSeconds = (elapsed.TotalSeconds / percent) * 100;
                                        double remainingSeconds = totalEstimatedSeconds - elapsed.TotalSeconds;
                                        eta = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
                                    }

                                    if (percent > 0)
                                    {
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
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _fileLogger.LogToFile($"Error parsing HandBrake progress: {ex.Message}", "ERROR");
                                }
                            }
                        }
                    };

                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    while (!process.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                        {
                            _fileLogger.LogToFile("Stop requested during transcode - killing HandBrake");
                            _logger.LogInformation("Stop requested - terminating HandBrake process");

                            try
                            {
                                if (!process.HasExited)
                                {
                                    process.Kill();
                                    process.WaitForExit(5000);
                                }
                                stoppedByUser = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error killing HandBrake process: {Error}", ex.Message);
                                _fileLogger.LogToFile($"Error killing HandBrake: {ex.Message}", "ERROR");
                            }
                            break;
                        }

                        await Task.Delay(500, cancellationToken);
                    }

                    if (!stoppedByUser && !process.HasExited)
                    {
                        process.WaitForExit();
                        process.WaitForExit();
                    }

                    _currentHandBrakeProcess = null;

                    if (stoppedByUser)
                    {
                        _fileLogger.LogToFile("Transcode stopped by user", "WARNING");

                        if (File.Exists(outputFile))
                        {
                            try
                            {
                                _fileLogger.LogToFile($"Deleting incomplete output file: {outputFile}");
                                File.Delete(outputFile);
                                _fileLogger.LogToFile("Incomplete file deleted successfully");
                            }
                            catch (Exception ex)
                            {
                                _fileLogger.LogToFile($"Failed to delete incomplete file: {ex.Message}", "ERROR");
                            }
                        }
                    }
                    else if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully transcoded: {Output}", outputFile);
                        _fileLogger.LogToFile($"Successfully transcoded: {outputFile}", "SUCCESS");
                        success = true;

                        if (_settings.DeleteOriginalAfterTranscode)
                        {
                            File.Delete(inputFile);
                            _logger.LogInformation("Deleted original: {Input}", inputFile);
                            _fileLogger.LogToFile($"Deleted original: {inputFile}");
                        }
                    }
                    else
                    {
                        _logger.LogError("Transcode failed with exit code: {ExitCode}", process.ExitCode);
                        _fileLogger.LogToFile($"Transcode failed with exit code: {process.ExitCode}", "ERROR");
                    }
                }
            }
            else if (canCreateOutputFile)
            {

                success = await copyVideoFile(inputFile, outputFile, cancellationToken);

            }
            else
            {
                _logger.LogInformation("Video creation skipped. Output file already exists");
                _fileLogger.LogToFile($"Video creation skipped. Output file already exists: {outputFile}");
            }

            HistoryManager.RemoveEntry(inputFile);
            HistoryManager.AddEntry(inputFile, outputFile, success);

            ProgressManager.ClearProgress();

            _currentOutputFile = null;

            _fileLogger.LogToFile($"========================================");
            _fileLogger.LogToFile($"Finished processing: {inputFile}");
            _fileLogger.LogToFile($"========================================");
        }

        private async Task<bool> copyVideoFile(string inputFile, string outputFile, CancellationToken cancellationToken)
        {
            //transcoding is disabled. just copy file
            _logger.LogInformation("Starting to copy: {Input} -> {Output}", inputFile, outputFile);
            _fileLogger.LogToFile($"========================================");
            _fileLogger.LogToFile($"Starting to copy: {inputFile}");
            _fileLogger.LogToFile($"Output: {outputFile}");
            _fileLogger.LogToFile($"========================================");

            // Set initial progress
            ProgressManager.Save(new TranscodeProgress
            {
                IsTranscoding = true,
                CurrentFile = inputFile,
                OutputFile = outputFile,
                PercentComplete = 0,
                StartTime = _currentTranscodeStartTime,
                Status = "Starting to copy file...",
                EstimatedTimeRemaining = TimeSpan.Zero
            });

            bool copied = false;

            try
            {
                await CopyFileWithProgressAsync(inputFile, outputFile, cancellationToken,
                    (progress) =>
                    {
                        ProgressManager.Save(new TranscodeProgress
                        {
                            IsTranscoding = true,
                            CurrentFile = inputFile,
                            OutputFile = outputFile,
                            PercentComplete = progress,
                            StartTime = _currentTranscodeStartTime,
                            Status = $"Copying file... {progress}%",
                            EstimatedTimeRemaining = TimeSpan.Zero
                        });
                    });
                copied = true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("File copy was cancelled: {Input} -> {Output}", inputFile, outputFile);
                _fileLogger.LogToFile($"File copy cancelled: {inputFile} -> {outputFile}", "CANCELLED");

                // CopyFileWithProgressAsync now handles cleanup itself
                // Just update progress to reflect cancellation
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = false,
                    CurrentFile = inputFile,
                    OutputFile = outputFile,
                    PercentComplete = 0,
                    StartTime = _currentTranscodeStartTime,
                    Status = "Copy cancelled",
                    EstimatedTimeRemaining = TimeSpan.Zero
                });

                // Add to history as failed
                //HistoryManager.RemoveEntry(inputFile);
                //HistoryManager.AddEntry(inputFile, outputFile, false);

                _currentOutputFile = null;

                // Clean up partial output file
                if (File.Exists(outputFile))
                {
                    try { File.Delete(outputFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("File copy failed: {Error}", ex.Message);
                _fileLogger.LogToFile($"File copy failed: {ex.Message}", "ERROR");
            }

            if (copied)
            {
                _logger.LogInformation("Successfully copied: {Output}", outputFile);
                _fileLogger.LogToFile($"Successfully copied: {outputFile}", "SUCCESS");
                if (_settings.DeleteOriginalAfterTranscode)
                {
                    File.Delete(inputFile);
                    _logger.LogInformation("Deleted original: {Input}", inputFile);
                    _fileLogger.LogToFile($"Deleted original: {inputFile}");
                }

                return true;
            }

            return false;
        }

        private async Task CopyFileWithProgressAsync(string sourceFile, string destinationFile, CancellationToken cancellationToken, Action<int> progressCallback = null)
        {
            const int bufferSize = 81920; // 80KB buffer
            var buffer = new byte[bufferSize];

            using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
            using var destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

            long totalBytes = source.Length;
            long totalBytesRead = 0;
            int bytesRead;
            int lastReportedProgress = 0;

            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                // Check for cancellation before writing
                if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                {
                    _fileLogger.LogToFile("Copy operation cancelled - cleaning up partial file");

                    // Close streams before trying to delete
                    destination.Close();
                    source.Close();

                    // Delete partial file
                    try
                    {
                        if (File.Exists(destinationFile))
                        {
                            File.Delete(destinationFile);
                            _fileLogger.LogToFile($"Deleted partial file: {destinationFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _fileLogger.LogToFile($"Failed to delete partial file: {ex.Message}", "WARNING");
                    }

                    throw new OperationCanceledException("Copy operation was cancelled");
                }

                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;

                // Report progress (only when it changes by at least 1%)
                int currentProgress = (int)((totalBytesRead * 100) / totalBytes);
                if (currentProgress != lastReportedProgress && progressCallback != null)
                {
                    lastReportedProgress = currentProgress;
                    progressCallback(currentProgress);
                }
            }

            // Final check before finalizing
            if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
            {
                _fileLogger.LogToFile("Copy operation cancelled at finalization");
                destination.Close();
                source.Close();

                try
                {
                    if (File.Exists(destinationFile))
                    {
                        File.Delete(destinationFile);
                        _fileLogger.LogToFile($"Deleted completed but cancelled file: {destinationFile}");
                    }
                }
                catch (Exception ex)
                {
                    _fileLogger.LogToFile($"Failed to delete file: {ex.Message}", "WARNING");
                }

                throw new OperationCanceledException("Copy operation was cancelled");
            }
        }



        //////////////////////////////ffmpeg transcoding
        ///

        private async Task<bool> ConvertUsingFFmpegAsync(string inputFile, string outputFile, CancellationToken cancellationToken)
        {
            if (!File.Exists(_settings.FFmpegPath))
            {
                _logger.LogError("FFmpeg not found at: {Path}", _settings.FFmpegPath);
                _fileLogger.LogToFile($"FFmpeg not found at: {_settings.FFmpegPath}", "ERROR");
                return false;
            }

            _logger.LogInformation("Analyzing file with FFmpeg: {Input}", inputFile);
            _fileLogger.LogToFile($"Analyzing file with FFmpeg: {inputFile}");

            try
            {
                // Probe the input file to get codec information
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
                    probeOutput = await probeProcess.StandardError.ReadToEndAsync();
                    probeProcess.WaitForExit();
                }

                // Parse video and audio codec information
                string videoCodec = ExtractVideoCodec(probeOutput);
                string audioCodec = ExtractAudioCodec(probeOutput);
                string audioChannels = ExtractAudioChannels(probeOutput);
                bool hasSubtitles = HasSubtitles(probeOutput);
                string currentContainer = Path.GetExtension(inputFile).TrimStart('.').ToLower();
                string targetContainer = _settings.OutputFileExtension.TrimStart('.').ToLower();

                _logger.LogInformation("Current: Container={Container}, Video={Video}, Audio={Audio}, Channels={Channels}, Subtitles included={HasSubtitles}",
                    currentContainer, videoCodec, audioCodec, audioChannels, hasSubtitles);
                _fileLogger.LogToFile($"Current format - Container: {currentContainer}, Video: {videoCodec}, Audio: {audioCodec}, Channels: {audioChannels}, Subtitles included: {hasSubtitles}");

                // Determine if transcoding is needed
                bool needsVideoTranscode = !IsVideoCodecValid(videoCodec);
                bool needsAudioTranscode = !IsAudioCodecValid(audioCodec) || NeedsAudioDownmix(audioChannels);
                bool needsRemux = currentContainer != targetContainer;
                bool needsSubtitleRemoval = hasSubtitles;

                if (!needsVideoTranscode && !needsAudioTranscode && !needsRemux && !needsSubtitleRemoval)
                {
                    _logger.LogInformation("File already in correct format, no conversion needed. File will be copied.");
                    _fileLogger.LogToFile("File already in correct format, no conversion needed. File will be copied.");

                    return await copyVideoFile(inputFile, outputFile, cancellationToken);
                }
                else
                {
                    _fileLogger.LogToFile($"Result of determining if transcoding needed: needsVideoTranscode: {needsVideoTranscode}, needsAudioTranscode: {needsAudioTranscode}, needsRemux: {needsRemux}, needsSubtitleRemoval: {needsSubtitleRemoval}");
                }

                bool includeSubtitles = false;
                string subtitleArgs = includeSubtitles ? "-map 0:s? -c:s copy" : "";
                string overwriteArg = _settings.OverwriteExistingVideos ? "-y" : "-n";

                // Build FFmpeg arguments
                int targetAudioChannels = GetTargetChannelCount(audioChannels);

                //try to match source audio channels (and cap them at 6), else default to 6
                string targetAudioChannelsArg = targetAudioChannels < 1 ? "6" : targetAudioChannels.ToString();

                string videoArgs = needsVideoTranscode ? "-c:v libx265 -preset medium -crf 23" : "-c:v copy";
                string audioArgs = needsAudioTranscode ? $"-c:a aac -b:a 384k -ac {targetAudioChannelsArg}" : "-c:a copy";

                string ffmpegArgs = $"-i \"{inputFile}\" {videoArgs} {audioArgs} -map 0:v -map 0:a {subtitleArgs} {overwriteArg} \"{outputFile}\"";

                _logger.LogInformation("Starting FFmpeg conversion: {Input} -> {Output}", inputFile, outputFile);
                _fileLogger.LogToFile($"========================================");
                _fileLogger.LogToFile($"Starting FFmpeg conversion: {inputFile}");
                _fileLogger.LogToFile($"Output: {outputFile}");
                _fileLogger.LogToFile($"Actions: Video={(needsVideoTranscode ? "Transcode to H.265" : "Copy")}, Audio={(needsAudioTranscode ? "Transcode to AAC 5.1" : "Copy")}, Container={(needsRemux ? "Remux to " + targetContainer : "Same")}, Subtitles={(needsSubtitleRemoval ? "Remove" : "None")}");
                _fileLogger.LogToFile($"FFmpeg command: {_settings.FFmpegPath} {ffmpegArgs}");
                _fileLogger.LogToFile($"========================================");

                // Check if output file already exists
                if (!_settings.OverwriteExistingVideos && File.Exists(outputFile))
                {
                    _logger.LogInformation("Output file already exists and overwrite is disabled: {Output}", outputFile);
                    _fileLogger.LogToFile($"Output file already exists and overwrite is disabled: {outputFile}", "WARNING");
                    return false;
                }

                // Set initial progress
                ProgressManager.Save(new TranscodeProgress
                {
                    IsTranscoding = true,
                    CurrentFile = inputFile,
                    OutputFile = outputFile,
                    PercentComplete = 0,
                    StartTime = _currentTranscodeStartTime,
                    Status = "Starting FFmpeg conversion...",
                    EstimatedTimeRemaining = TimeSpan.Zero
                });

                var startInfo = new ProcessStartInfo
                {
                    FileName = _settings.FFmpegPath,
                    Arguments = ffmpegArgs,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                bool stoppedByUser = false;
                bool success = false;

                using (var process = Process.Start(startInfo))
                {
                    _currentFFmpegProcess = process;
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;

                    // Get total duration for progress calculation
                    double totalDuration = ExtractDuration(probeOutput);

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            _fileLogger.LogToFile(e.Data, "FFMPEG");

                            // Parse progress from FFmpeg output
                            if (e.Data.Contains("time=") && totalDuration > 0)
                            {
                                try
                                {
                                    var timeMatch = System.Text.RegularExpressions.Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.\d+)");
                                    if (timeMatch.Success)
                                    {
                                        int hours = int.Parse(timeMatch.Groups[1].Value);
                                        int minutes = int.Parse(timeMatch.Groups[2].Value);
                                        double seconds = double.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture);

                                        double currentTime = hours * 3600 + minutes * 60 + seconds;
                                        double percent = (currentTime / totalDuration) * 100;
                                        percent = Math.Min(99.9, Math.Max(0, percent));

                                        TimeSpan elapsed = DateTime.Now - _currentTranscodeStartTime;
                                        TimeSpan eta = TimeSpan.Zero;

                                        if (percent > 0 && percent < 100)
                                        {
                                            double totalEstimatedSeconds = (elapsed.TotalSeconds / percent) * 100;
                                            double remainingSeconds = totalEstimatedSeconds - elapsed.TotalSeconds;
                                            eta = TimeSpan.FromSeconds(Math.Max(0, remainingSeconds));
                                        }

                                        ProgressManager.Save(new TranscodeProgress
                                        {
                                            IsTranscoding = true,
                                            CurrentFile = inputFile,
                                            OutputFile = outputFile,
                                            PercentComplete = percent,
                                            StartTime = _currentTranscodeStartTime,
                                            Status = $"Converting: {percent:F1}%",
                                            EstimatedTimeRemaining = eta
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _fileLogger.LogToFile($"Error parsing FFmpeg progress: {ex.Message}", "ERROR");
                                }
                            }
                        }
                    };

                    process.BeginErrorReadLine();

                    while (!process.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested || ServiceControl.IsStopRequested())
                        {
                            _fileLogger.LogToFile("Stop requested during FFmpeg conversion - killing process");
                            _logger.LogInformation("Stop requested - terminating FFmpeg process");

                            try
                            {
                                if (!process.HasExited)
                                {
                                    process.Kill();
                                    process.WaitForExit(5000);
                                }
                                stoppedByUser = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error killing FFmpeg process: {Error}", ex.Message);
                                _fileLogger.LogToFile($"Error killing FFmpeg: {ex.Message}", "ERROR");
                            }
                            break;
                        }

                        await Task.Delay(500, cancellationToken);
                    }

                    if (!stoppedByUser && !process.HasExited)
                    {
                        process.WaitForExit();
                    }

                    _currentFFmpegProcess = null;

                    if (stoppedByUser)
                    {
                        _fileLogger.LogToFile("FFmpeg conversion stopped by user", "WARNING");

                        if (File.Exists(outputFile))
                        {
                            try
                            {
                                _fileLogger.LogToFile($"Deleting incomplete output file: {outputFile}");
                                File.Delete(outputFile);
                                _fileLogger.LogToFile("Incomplete file deleted successfully");
                            }
                            catch (Exception ex)
                            {
                                _fileLogger.LogToFile($"Failed to delete incomplete file: {ex.Message}", "ERROR");
                            }
                        }
                    }
                    else if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Successfully converted with FFmpeg: {Output}", outputFile);
                        _fileLogger.LogToFile($"Successfully converted with FFmpeg: {outputFile}", "SUCCESS");
                        success = true;
                    }
                    else
                    {
                        _logger.LogError("FFmpeg conversion failed with exit code: {ExitCode}", process.ExitCode);
                        _fileLogger.LogToFile($"FFmpeg conversion failed with exit code: {process.ExitCode}", "ERROR");
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during FFmpeg conversion");
                _fileLogger.LogToFile($"Error during FFmpeg conversion: {ex.Message}", "ERROR");
                return false;
            }
        }

        private int GetTargetChannelCount(string extracted)
        {
            int channels = ResolveChannelCount(extracted);

            if (channels < 0)
                return -1;

            if (channels > 6)
                return 6;

            return channels;
        }
        private int ResolveChannelCount(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "unknown")
                return -1; 

            raw = raw.ToLowerInvariant();

            // map known names
            if (raw == "mono") return 1;
            if (raw == "stereo") return 2;

            // map known layouts with dots
            if (raw == "5.1") return 6;
            if (raw == "7.1") return 8;

            raw = raw.Replace(',', '.'); // normalize to '.'
            // NumberStyles.Float, CultureInfo.InvariantCulture correctly handles both '.' and ',' as decimal separators
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return (int)Math.Ceiling(d);

            return -1;
        }

        private string ExtractVideoCodec(string ffmpegOutput)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                ffmpegOutput,
                @"Stream #\d+:\d+.*?: Video: ([^\s,]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            return match.Success ? match.Groups[1].Value.ToLower() : "unknown";
        }

        private string ExtractAudioCodec(string ffmpegOutput)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                ffmpegOutput,
                @"Stream #\d+:\d+.*?: Audio: ([^\s,]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            return match.Success ? match.Groups[1].Value.ToLower() : "unknown";
        }

        private string ExtractAudioChannels(string ffmpegOutput)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                ffmpegOutput,
                @"Audio:.*?(\d+\.\d+|\d+)\s*channels?|Audio:.*?(mono|stereo|5\.1|7\.1)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                if (!string.IsNullOrEmpty(match.Groups[1].Value))
                    return match.Groups[1].Value;
                if (!string.IsNullOrEmpty(match.Groups[2].Value))
                    return match.Groups[2].Value;
            }

            return "unknown";
        }

        private double ExtractDuration(string ffmpegOutput)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                ffmpegOutput,
                @"Duration: (\d+):(\d+):(\d+\.\d+)"
            );

            if (match.Success)
            {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                double seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                return hours * 3600 + minutes * 60 + seconds;
            }

            return 0;
        }

        private bool IsVideoCodecValid(string codec)
        {
            return codec == "h264" || codec == "hevc" || codec == "h265";
        }

        private bool IsAudioCodecValid(string codec)
        {
            return codec == "aac" || codec == "ac3";
        }

        private bool NeedsAudioDownmix(string channels)
        {
            channels = channels.Replace(',', '.'); // normalize to '.'
            // Parse channel count and check if it's more than 5.1 (6 channels)
            if (double.TryParse(channels, NumberStyles.Float, CultureInfo.InvariantCulture, out double channelCount))
            {
                return channelCount > 6;
            }

            // Handle text descriptions
            string ch = channels.ToLower();
            if (ch.Contains("7.1") || ch.Contains("7.2"))
                return true;

            return false;
        }

        private bool HasSubtitles(string ffmpegOutput)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                ffmpegOutput,
                @"Stream #\d+:\d+.*?: Subtitle:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            return match.Success;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////

    }
}