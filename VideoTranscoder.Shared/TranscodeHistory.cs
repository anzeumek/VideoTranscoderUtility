using System;
using System.Collections.Generic;

namespace VideoTranscoder.Shared
{
    public class TranscodeHistoryEntry
    {
        public string SourceFilePath { get; set; }
        public string OutputFilePath { get; set; }
        public DateTime TranscodedDate { get; set; }
        public bool Success { get; set; }
    }

    public class TranscodeHistory
    {
        public List<TranscodeHistoryEntry> Entries { get; set; } = new List<TranscodeHistoryEntry>();
    }
}
