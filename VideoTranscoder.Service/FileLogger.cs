using System;
using System.Collections.Generic;
using System.Text;

namespace VideoTranscoder.Service
{
    public class FileLogger
    {
        private readonly ILogger _logger;
        private readonly string _logDirectory;
        private readonly string _logFileName;
        private const long MaxLogSize = 10 * 1024 * 1024; // 10MB
        private const int MaxLogFiles = 5;

        public FileLogger(ILogger logger, string logFileName = "service.log")
        {
            _logger = logger;
            _logFileName = logFileName;
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoTranscoder");

            EnsureLogDirectoryExists();
        }

        private void EnsureLogDirectoryExists()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public void LogToFile(string message, string level = "INFO")
        {
            try
            {
                string logPath = Path.Combine(_logDirectory, _logFileName);

                // Check file size and rotate if needed
                if (File.Exists(logPath))
                {
                    FileInfo logFile = new FileInfo(logPath);
                    if (logFile.Length > MaxLogSize)
                    {
                        RotateLogFile(logPath);
                    }
                }

                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Try to log to the system logger
                _logger?.LogWarning(ex, "Failed to write to log file");

                //As last resort, write to Windows Event Log
                try
                {
                    //write to Windows Event Log
#pragma warning disable CA1416 // Validate platform compatibility
                    System.Diagnostics.EventLog.WriteEntry(
                        "VideoTranscoder",
                        $"Logging failed: {ex.Message}\nOriginal message: {message}",
                        System.Diagnostics.EventLogEntryType.Warning);
#pragma warning restore CA1416 // Validate platform compatibility
                }
                catch
                {
                    // Give up
                }
            }
        }

        private void RotateLogFile(string currentLogPath)
        {
            try
            {
                // Archive current log with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string archivedLogPath = Path.Combine(_logDirectory, $"service_{timestamp}.log");

                // Move current log to archived name
                File.Move(currentLogPath, archivedLogPath);
                _logger.LogInformation("Log file rotated to: {ArchivedLog}", archivedLogPath);

                // Clean up old log files if we have more than maxLogFiles
                var logFiles = Directory.GetFiles(_logDirectory, "service_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (logFiles.Count > MaxLogFiles)
                {
                    foreach (var oldLog in logFiles.Skip(MaxLogFiles))
                    {
                        try
                        {
                            oldLog.Delete();
                            _logger.LogInformation("Deleted old log file: {OldLog}", oldLog.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to delete old log file {File}: {Error}",
                                oldLog.Name, ex.Message);
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
