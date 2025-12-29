using System;

namespace VideoTranscoder.Shared
{
    public class TranscodeProgress
    {
        public bool IsTranscoding { get; set; }
        public string CurrentFile { get; set; }
        public string OutputFile { get; set; }
        public double PercentComplete { get; set; }
        public DateTime StartTime { get; set; }
        public string Status { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double CurrentFPS { get; set; } // Frames per second


        // Subtitle extraction progress
        public bool IsExtractingSubtitles { get; set; }
        public string SubtitleExtractionStatus { get; set; }
        public int TotalSubtitleStreams { get; set; }
        public int ProcessedSubtitleStreams { get; set; }
    }
}
