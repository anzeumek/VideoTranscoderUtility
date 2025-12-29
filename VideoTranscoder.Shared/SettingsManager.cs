using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace VideoTranscoder.Shared
{
    public static class SettingsManager
    {
        private static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoTranscoder", "settings.json");

        public static TranscoderSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<TranscoderSettings>(json) ?? new TranscoderSettings();
                }
            }
            catch (Exception ex)
            {
                // Log error - in service write to Event Log, in GUI show message
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new TranscoderSettings();
        }

        public static bool Save(TranscoderSettings settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                return false;
            }
        }
    }
}
