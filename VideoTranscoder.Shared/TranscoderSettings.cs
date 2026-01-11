using System.Text.Json.Serialization;

namespace VideoTranscoder.Shared
{
    public class TranscoderSettings
    {
        public List<string> MonitoredDirectories { get; set; } = new List<string>();
        public string OutputDirectory { get; set; } = @"C:\TranscodedVideos";
        public string OutputFileExtension { get; set; } = "mp4";
        public bool PreserveFolderStructure { get; set; } = true;
        public string HandBrakePath { get; set; } = @"C:\Program Files\HandBrake\HandBrakeCLI.exe";
        public bool EnableTranscoding { get; set; } = true;
        public string FFmpegPath { get; set; } = @"C:\Program Files\ffmpeg\bin\ffmpeg.exe";
        public string HandBrakeParameters { get; set; } = "--format av_mp4 --crop-mode none --encoder x265 --quality 20 --keep-metadata --drc 2.0 --aencoder copy:aac --audio-lang-list slv,eng --subtitle-lang-list slv,eng --all-subtitles";
        public bool OverwriteExistingVideos { get; set; } = false;

        //Dev settings
        public bool ConvertUsingFFmpeg { get; set; } = false;

        // Scheduling
        public bool RunOnSchedule { get; set; } = false;
        public TimeSpan ScheduleStartTime { get; set; } = new TimeSpan(5, 0, 0); // 5 AM
        public TimeSpan ScheduleEndTime { get; set; } = new TimeSpan(6, 0, 0); // 6 AM
        public int CheckIntervalMinutes { get; set; } = 5; // How often to check for new files during the window

        // Days of week
        public bool RunOnMonday { get; set; } = true;
        public bool RunOnTuesday { get; set; } = true;
        public bool RunOnWednesday { get; set; } = true;
        public bool RunOnThursday { get; set; } = true;
        public bool RunOnFriday { get; set; } = true;
        public bool RunOnSaturday { get; set; } = true;
        public bool RunOnSunday { get; set; } = true;

        // Subtitle extraction settings
        public bool ExtractSubtitles { get; set; } = false;
        public string SubtitleFormats { get; set; } = "srt, ass, vtt"; // Comma-separated
        public bool ConvertToSrtIfMissing { get; set; } = true;
        public bool CopyExternalSubtitles { get; set; } = true;
        public string SubtitleLanguages { get; set; } = "en, sl"; // Comma-separated 2-letter ISO 639-1
        public bool OverwriteExistingSubtitles { get; set; } = false;

        //Subtitle download settings
        public bool DownloadSubtitles { get; set; } = false;
        public string OpenSubtitlesAppName { get; set; } = "";
        public string OpenSubtitlesApiKey { get; set; } = "";
        public string OpenSubtitlesUsername { get; set; } = "";
        public string OpenSubtitlesPassword { get; set; } = "";

        public bool FixCorruptedSubtitles { get; set; } = false;


        // Processing rules
        public bool DeleteOriginalAfterTranscode { get; set; } = false;
        public List<string> FileExtensions { get; set; } = new List<string> { ".mp4", ".avi", ".mkv", ".mov" };

        // List of subtitles language codes
        [JsonIgnore]
        public Dictionary<string, string> SubtitleLanguagesDictionary { get; set; } = new Dictionary<string, string>();


        public List<string> Validate()
        {
            var errors = new List<string>();

            // Check HandBrake exists
            if (EnableTranscoding && !File.Exists(HandBrakePath))
            {
                errors.Add($"HandBrake not found at: {HandBrakePath}");
            }

            // Check FFmpeg exists if subtitle extraction is enabled
            if (ExtractSubtitles && !File.Exists(FFmpegPath))
            {
                errors.Add($"FFmpeg not found at: {FFmpegPath} (required for subtitle extraction)");
            }

            // Check monitored directories
            if (MonitoredDirectories == null || !MonitoredDirectories.Any())
            {
                errors.Add("No monitored directories configured");
            }
            else
            {
                foreach (var dir in MonitoredDirectories)
                {
                    if (!Directory.Exists(dir))
                    {
                        errors.Add($"Monitored directory does not exist: {dir}");
                    }
                }
            }

            // Check output directory is writable
            try
            {
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }

                // Test write permission
                var testFile = Path.Combine(OutputDirectory, ".write_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                errors.Add($"Output directory not writable: {OutputDirectory} - {ex.Message}");
            }

            // Validate schedule times
            if (CheckIntervalMinutes < 1)
            {
                errors.Add("Check interval must be at least 1 minute");
            }

            // Check at least one day is enabled if schedule is on
            if (RunOnSchedule)
            {
                if (!RunOnMonday && !RunOnTuesday && !RunOnWednesday && !RunOnThursday &&
                    !RunOnFriday && !RunOnSaturday && !RunOnSunday)
                {
                    errors.Add("Schedule is enabled but no days are selected");
                }
            }

            // Validate OpenSubtitles settings if subtitle download is enabled
            if (DownloadSubtitles)
            {
                if (string.IsNullOrWhiteSpace(OpenSubtitlesAppName))
                {
                    errors.Add("OpenSubtitles App name is required when subtitle download is enabled");
                }
                if (string.IsNullOrWhiteSpace(OpenSubtitlesApiKey))
                {
                    errors.Add("OpenSubtitles API key is required when subtitle download is enabled");
                }
                if ((!string.IsNullOrWhiteSpace(OpenSubtitlesUsername) && string.IsNullOrWhiteSpace(OpenSubtitlesPassword)) || (string.IsNullOrWhiteSpace(OpenSubtitlesUsername) && !string.IsNullOrWhiteSpace(OpenSubtitlesPassword)))
                {
                    errors.Add("OpenSubtitles username and password should both be provided for login to work");
                }
            }

            // Validate file extensions
            if (FileExtensions == null || !FileExtensions.Any())
            {
                errors.Add("No file extensions configured for monitoring");
            }

            return errors;
        }

        //Check for warnings
        public List<string> GetWarnings()
        {
            var warnings = new List<string>();

            // Warn if delete original is enabled
            if (DeleteOriginalAfterTranscode)
            {
                warnings.Add("WARNING: Delete original files after transcode is ENABLED. Original files will be permanently deleted!");
            }

            // Warn if output directory overlaps with monitored directory
            var outputDir = Path.GetFullPath(OutputDirectory)
                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;

            foreach (var dir in MonitoredDirectories)
            {
                var monitoredDir = Path.GetFullPath(dir)
                                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                       + Path.DirectorySeparatorChar;

                if (outputDir.StartsWith(monitoredDir, StringComparison.OrdinalIgnoreCase) ||
                    monitoredDir.StartsWith(outputDir, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"WARNING: Output directory overlaps with monitored directory: {dir}. " +
                        "This may cause files to be processed multiple times."
                    );
                }
            }


            return warnings;
        }

        // Helper method to get subtitle formats as a list
        public List<string> GetSubtitleFormatsList()
        {
            if (string.IsNullOrWhiteSpace(SubtitleFormats))
                return new List<string>();

            return SubtitleFormats
                .Split(',')
                .Select(f => f.Trim().ToLower())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
        }

        // Helper method to get subtitle languages as a list
        public List<string> GetSubtitleLanguagesList()
        {
            if (string.IsNullOrWhiteSpace(SubtitleLanguages))
                return new List<string>();

            return SubtitleLanguages
                .Split(',')
                .Select(f => f.Trim().ToLower())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
        }

        // Helper method to check if today is a valid day
        public bool IsValidDay(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => RunOnMonday,
                DayOfWeek.Tuesday => RunOnTuesday,
                DayOfWeek.Wednesday => RunOnWednesday,
                DayOfWeek.Thursday => RunOnThursday,
                DayOfWeek.Friday => RunOnFriday,
                DayOfWeek.Saturday => RunOnSaturday,
                DayOfWeek.Sunday => RunOnSunday,
                _ => false
            };
        }
    }
}
