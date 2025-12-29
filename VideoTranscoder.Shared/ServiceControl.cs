using System;
using System.IO;

namespace VideoTranscoder.Shared
{
    public static class ServiceControl
    {
        private static readonly string StopFilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoTranscoder", "stop.signal");

        public static void RequestStop()
        {
            try
            {
                string directory = Path.GetDirectoryName(StopFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(StopFilePath, DateTime.Now.ToString());
            }
            catch
            {
                // Ignore errors
            }
        }

        public static bool IsStopRequested()
        {
            return File.Exists(StopFilePath);
        }

        public static void ClearStopRequest()
        {
            try
            {
                if (File.Exists(StopFilePath))
                    File.Delete(StopFilePath);
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
