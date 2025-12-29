using System;
using System.Collections.Generic;
using System.Text;

namespace VideoTranscoder.Shared
{
    public class TranscoderSettings
    {
        public List<string> MonitoredDirectories { get; set; } = new List<string>();
        public string OutputDirectory { get; set; } = @"C:\TranscodedVideos";
        public string OutputFileExtension { get; set; } = "mp4";
        public bool PreserveFolderStructure { get; set; } = true;
        public string HandBrakePath { get; set; } = @"C:\Program Files\HandBrake\HandBrakeCLI.exe";

        // Transcoding rules
        //public int MaxResolutionHeight { get; set; } = 1080;
        //public string TargetCodec { get; set; } = "H.265";
        //public int TargetBitrate { get; set; } = 2500; // kbps

        public string FFmpegPath { get; set; } = @"C:\Program Files\ffmpeg\bin\ffmpeg.exe";
        public string HandBrakeParameters { get; set; } = "--format av_mp4 --crop-mode none --encoder x265 --quality 21 --keep-metadata --drc 2.0 --aencoder copy:aac --audio-lang-list slv,eng --subtitle-lang-list slv,eng --all-subtitles";

        // Scheduling
        public bool RunOnSchedule { get; set; } = true;
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


        // Processing rules
        public bool DeleteOriginalAfterTranscode { get; set; } = false;
        public List<string> FileExtensions { get; set; } = new List<string> { ".mp4", ".avi", ".mkv", ".mov" };


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
