using System;
using System.IO;
using System.Text.Json;

namespace VideoTranscoder.Shared
{
    public static class ProgressManager
    {
        private static readonly string ProgressPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoTranscoder", "progress.json");

        public static TranscodeProgress Load()
        {
            try
            {
                if (File.Exists(ProgressPath))
                {
                    string json = File.ReadAllText(ProgressPath);
                    return JsonSerializer.Deserialize<TranscodeProgress>(json) ?? new TranscodeProgress();
                }
            }
            catch
            {
                // Ignore errors, return default
            }

            return new TranscodeProgress();
        }

        public static void Save(TranscodeProgress progress)
        {
            try
            {
                string directory = Path.GetDirectoryName(ProgressPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(progress, options);
                File.WriteAllText(ProgressPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        public static void ClearProgress()
        {
            Save(new TranscodeProgress { IsTranscoding = false, Status = "Idle" });
        }
    }
}