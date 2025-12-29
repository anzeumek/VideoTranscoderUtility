using System;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace VideoTranscoder.Shared
{
    public static class HistoryManager
    {
        private static readonly string HistoryPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoTranscoder", "history.json");

        public static TranscodeHistory Load()
        {
            try
            {
                if (File.Exists(HistoryPath))
                {
                    string json = File.ReadAllText(HistoryPath);
                    return JsonSerializer.Deserialize<TranscodeHistory>(json) ?? new TranscodeHistory();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading history: {ex.Message}");
            }

            return new TranscodeHistory();
        }

        public static bool Save(TranscodeHistory history)
        {
            try
            {
                string directory = Path.GetDirectoryName(HistoryPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(history, options);
                File.WriteAllText(HistoryPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving history: {ex.Message}");
                return false;
            }
        }

        public static bool IsAlreadyTranscoded(string filePath)
        {
            var history = Load();
            return history.Entries.Any(e =>
                e.SourceFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) &&
                e.Success);
        }

        public static void AddEntry(string sourceFile, string outputFile, bool success)
        {
            var history = Load();
            history.Entries.Add(new TranscodeHistoryEntry
            {
                SourceFilePath = sourceFile,
                OutputFilePath = outputFile,
                TranscodedDate = DateTime.Now,
                Success = success
            });
            Save(history);
        }

        public static bool RemoveEntry(string sourceFilePath)
        {
            var history = Load();
            var entry = history.Entries.FirstOrDefault(e =>
                e.SourceFilePath.Equals(sourceFilePath, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                history.Entries.Remove(entry);
                return Save(history);
            }
            return false;
        }

        public static bool ClearAll()
        {
            try
            {
                var emptyHistory = new TranscodeHistory();
                return Save(emptyHistory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing history: {ex.Message}");
                return false;
            }
        }
    }
}