using System.Diagnostics;
using System.Runtime;
using System.Windows.Forms;
using VideoTranscoder.Shared;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace VideoTranscoder.GUI
{
    public partial class Form1 : Form
    {
        private const string ServiceName = "VideoTranscoderService";
        private TranscoderSettings _settings;
        private System.Windows.Forms.Timer _progressTimer;

        // Add these lines here with your other fields
        private string _historySortColumn = "Date";
        private bool _historySortAscending = false;

        public Form1()
        {
            InitializeComponent();
            LoadSettings();
            LoadHistory();
            LoadLog();

            // Set up timer to check progress every 2 seconds
            _progressTimer = new System.Windows.Forms.Timer();
            _progressTimer.Interval = 2000; // 2 seconds
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            // Initial service status check
            UpdateServiceStatus();
        }


        private void LoadSettings()
        {
            _settings = SettingsManager.Load();

            // Populate UI controls with settings
            listBoxDirectories.Items.Clear();
            foreach (var dir in _settings.MonitoredDirectories)
                listBoxDirectories.Items.Add(dir);

            txtOutputDir.Text = _settings.OutputDirectory;
            txtHandBrakePath.Text = _settings.HandBrakePath;
            cbEnableTranscoding.Checked = _settings.EnableTranscoding;
            chkOverwriteExistingVideos.Checked = _settings.OverwriteExistingVideos;
            txtOutputFileExtension.Text = _settings.OutputFileExtension;
            chkPreserveFolderStructure.Checked = _settings.PreserveFolderStructure;
            txtParameters.Text = _settings.HandBrakeParameters;
            chkRunOnSchedule.Checked = _settings.RunOnSchedule;

            timeScheduleStart.Value = DateTime.Today.Add(_settings.ScheduleStartTime);
            timeScheduleEnd.Value = DateTime.Today.Add(_settings.ScheduleEndTime);
            numCheckInterval.Value = _settings.CheckIntervalMinutes;

            chkMonday.Checked = _settings.RunOnMonday;
            chkTuesday.Checked = _settings.RunOnTuesday;
            chkWednesday.Checked = _settings.RunOnWednesday;
            chkThursday.Checked = _settings.RunOnThursday;
            chkFriday.Checked = _settings.RunOnFriday;
            chkSaturday.Checked = _settings.RunOnSaturday;
            chkSunday.Checked = _settings.RunOnSunday;

            // Subtitle settings
            cbDownloadMissingSubs.Checked = _settings.DownloadSubtitles;
            txtFFmpegPath.Text = _settings.FFmpegPath;
            chkExtractSubtitles.Checked = _settings.ExtractSubtitles;
            txtSubtitleFormats.Text = _settings.SubtitleFormats;
            chkConvertToSrt.Checked = _settings.ConvertToSrtIfMissing;
            chkCopyExternalSubs.Checked = _settings.CopyExternalSubtitles;
            txtSubtitleLanguages.Text = _settings.SubtitleLanguages;
            chkOverwriteExistingSubtitles.Checked = _settings.OverwriteExistingSubtitles;

            // Subtitle download settings
            txtOpenSubtitlesAppName.Text = _settings.OpenSubtitlesAppName;
            txtOpenSubtitlesApiKey.Text = _settings.OpenSubtitlesApiKey;
            txtOpenSubtitlesUsername.Text = _settings.OpenSubtitlesUsername;
            txtOpenSubtitlesPassword.Text = _settings.OpenSubtitlesPassword;

            chkDeleteOriginal.Checked = _settings.DeleteOriginalAfterTranscode;
        }

        private void btnRemoveDirectory_Click(object sender, EventArgs e)
        {
            if (listBoxDirectories.SelectedIndex >= 0)
            {
                _settings.MonitoredDirectories.RemoveAt(listBoxDirectories.SelectedIndex);
                listBoxDirectories.Items.RemoveAt(listBoxDirectories.SelectedIndex);
            }
        }

        private void btnAddDirectory_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _settings.MonitoredDirectories.Add(dialog.SelectedPath);
                    listBoxDirectories.Items.Add(dialog.SelectedPath);
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {

            // Update settings from UI
            _settings.OutputDirectory = txtOutputDir.Text;
            _settings.HandBrakePath = txtHandBrakePath.Text;
            _settings.EnableTranscoding = cbEnableTranscoding.Checked;
            _settings.OverwriteExistingVideos = chkOverwriteExistingVideos.Checked;
            _settings.OutputFileExtension = txtOutputFileExtension.Text;
            _settings.PreserveFolderStructure = chkPreserveFolderStructure.Checked;
            _settings.HandBrakeParameters = txtParameters.Text;
            _settings.RunOnSchedule = chkRunOnSchedule.Checked;

            // Save time window
            _settings.ScheduleStartTime = timeScheduleStart.Value.TimeOfDay;
            _settings.ScheduleEndTime = timeScheduleEnd.Value.TimeOfDay;
            _settings.CheckIntervalMinutes = (int)numCheckInterval.Value;

            // Save days of week
            _settings.RunOnMonday = chkMonday.Checked;
            _settings.RunOnTuesday = chkTuesday.Checked;
            _settings.RunOnWednesday = chkWednesday.Checked;
            _settings.RunOnThursday = chkThursday.Checked;
            _settings.RunOnFriday = chkFriday.Checked;
            _settings.RunOnSaturday = chkSaturday.Checked;
            _settings.RunOnSunday = chkSunday.Checked;

            // Subtitle settings
            _settings.FFmpegPath = txtFFmpegPath.Text;
            _settings.ExtractSubtitles = chkExtractSubtitles.Checked;
            _settings.SubtitleFormats = txtSubtitleFormats.Text;
            _settings.ConvertToSrtIfMissing = chkConvertToSrt.Checked;
            _settings.CopyExternalSubtitles = chkCopyExternalSubs.Checked;
            _settings.SubtitleLanguages = txtSubtitleLanguages.Text;
            _settings.OverwriteExistingSubtitles = chkOverwriteExistingSubtitles.Checked;

            // Subtitle download settings
            _settings.DownloadSubtitles = cbDownloadMissingSubs.Checked;
            _settings.OpenSubtitlesAppName = txtOpenSubtitlesAppName.Text;
            _settings.OpenSubtitlesApiKey = txtOpenSubtitlesApiKey.Text;
            _settings.OpenSubtitlesUsername = txtOpenSubtitlesUsername.Text;
            _settings.OpenSubtitlesPassword = txtOpenSubtitlesPassword.Text;

            // Source video file
            _settings.DeleteOriginalAfterTranscode = chkDeleteOriginal.Checked;

            if (SettingsManager.Save(_settings))
            {
                string scheduleInfo = "";
                if (_settings.RunOnSchedule)
                {
                    var days = new List<string>();
                    if (_settings.RunOnMonday) days.Add("Mon");
                    if (_settings.RunOnTuesday) days.Add("Tue");
                    if (_settings.RunOnWednesday) days.Add("Wed");
                    if (_settings.RunOnThursday) days.Add("Thu");
                    if (_settings.RunOnFriday) days.Add("Fri");
                    if (_settings.RunOnSaturday) days.Add("Sat");
                    if (_settings.RunOnSunday) days.Add("Sun");

                    string daysList = string.Join(", ", days);
                    scheduleInfo = $"\n\nScheduled to run on: {daysList}\nFrom {_settings.ScheduleStartTime:hh\\:mm} to {_settings.ScheduleEndTime:hh\\:mm}";
                }

                MessageBox.Show($"Settings saved successfully!{scheduleInfo}\n\nThe service will use these settings immediately.",
                              "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to save settings. Check permissions.",
                              "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadHistory()
        {
            /*
            var history = HistoryManager.Load();

            dgvHistory.DataSource = history.Entries
                .OrderByDescending(e => e.TranscodedDate)
                .Select(e => new
                {
                    SourceFile = e.SourceFilePath,
                    OutputFile = e.OutputFilePath,
                    Date = e.TranscodedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = e.Success ? "Success" : "Failed"
                })
                .ToList();

            dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            */
            LoadHistorySorted();
        }

        private void dgvHistory_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            string clickedColumn = dgvHistory.Columns[e.ColumnIndex].Name;

            // Toggle sort direction if same column clicked
            if (clickedColumn == _historySortColumn)
            {
                _historySortAscending = !_historySortAscending;
            }
            else
            {
                _historySortColumn = clickedColumn;
                _historySortAscending = true;
            }

            // Reload with new sort
            LoadHistorySorted();
        }

        private void LoadHistorySorted()
        {
            var history = HistoryManager.Load();

            var query = history.Entries.Select(e => new
            {
                SourceFile = e.SourceFilePath,
                OutputFile = e.OutputFilePath,
                Date = e.TranscodedDate,
                Status = e.Success ? "Success" : "Failed"
            });

            // Apply sorting based on column and direction
            IEnumerable<dynamic> sorted = _historySortColumn switch
            {
                "SourceFile" => _historySortAscending
                    ? query.OrderBy(x => x.SourceFile)
                    : query.OrderByDescending(x => x.SourceFile),
                "OutputFile" => _historySortAscending
                    ? query.OrderBy(x => x.OutputFile)
                    : query.OrderByDescending(x => x.OutputFile),
                "Date" => _historySortAscending
                    ? query.OrderBy(x => x.Date)
                    : query.OrderByDescending(x => x.Date),
                "Status" => _historySortAscending
                    ? query.OrderBy(x => x.Status)
                    : query.OrderByDescending(x => x.Status),
                _ => query.OrderByDescending(x => x.Date)
            };

            dgvHistory.DataSource = sorted.ToList();
            dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // Update column headers to show sort direction
            foreach (DataGridViewColumn column in dgvHistory.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }

            if (dgvHistory.Columns[_historySortColumn] != null)
            {
                dgvHistory.Columns[_historySortColumn].HeaderCell.SortGlyphDirection =
                    _historySortAscending ? SortOrder.Ascending : SortOrder.Descending;
            }

            // Set column headers
            if (dgvHistory.Columns["SourceFile"] != null)
                dgvHistory.Columns["SourceFile"].HeaderText = "Source File";

            if (dgvHistory.Columns["OutputFile"] != null)
                dgvHistory.Columns["OutputFile"].HeaderText = "Output File";

            if (dgvHistory.Columns["Date"] != null)
            {
                dgvHistory.Columns["Date"].HeaderText = "Processed Date";
                dgvHistory.Columns["Date"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";
            }

            if (dgvHistory.Columns["Status"] != null)
                dgvHistory.Columns["Status"].HeaderText = "Status";
        }

        private void btnRefreshHistory_Click(object sender, EventArgs e)
        {
            LoadHistory();
        }

        private void btnRemoveHistory_Click(object sender, EventArgs e)
        {
            if (dgvHistory.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a file to remove.", "No Selection",
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedRow = dgvHistory.SelectedRows[0];
            string sourceFile = selectedRow.Cells["SourceFile"].Value.ToString();
            string status = selectedRow.Cells["Status"].Value.ToString();

            string message = $"Remove this file from history?\n\n{sourceFile}\n\n. Removing it will allow file to be processed again.";

            var result = MessageBox.Show(
                message,
                "Confirm Removal",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (HistoryManager.RemoveEntry(sourceFile))
                {
                    MessageBox.Show("Entry removed successfully.", "Success",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadHistory();
                }
                else
                {
                    MessageBox.Show("Failed to remove entry. Make sure that you have the right privileges.", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnClearHistory_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear ALL history?\n\n" +
                "This will allow all previously processed files to be processed again.",
                "Confirm Clear All",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                var confirmResult = MessageBox.Show(
                    "This action cannot be undone. Continue?",
                    "Final Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirmResult == DialogResult.Yes)
                {
                    if (HistoryManager.ClearAll())
                    {
                        MessageBox.Show("All history cleared successfully.", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadHistory();
                    }
                    else
                    {
                        MessageBox.Show("Failed to clear history. Make sure that you have the right privileges.", "Error",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            UpdateProgress();
            UpdateServiceStatus();
        }

        private void UpdateProgress()
        {
            var progress = ProgressManager.Load();

            if (progress.IsTranscoding)
            {
                lblStatus.Text = $"Status: {progress.Status}";
                lblCurrentFile.Text = $"Current File: {Path.GetFileName(progress.CurrentFile)}";
                progressBar1.Value = Math.Min((int)progress.PercentComplete, 100);
                lblPercent.Text = $"{progress.PercentComplete:F1}%";

                // Show ETA
                if (progress.EstimatedTimeRemaining.TotalSeconds > 0)
                {
                    lblETA.Text = $"Estimated Time Remaining: {FormatTimeSpan(progress.EstimatedTimeRemaining)}";
                }
                else
                {
                    lblETA.Text = "Estimated Time Remaining: Calculating...";
                }

                // Show elapsed time
                TimeSpan elapsed = DateTime.Now - progress.StartTime;
                lblElapsed.Text = $"Elapsed: {FormatTimeSpan(elapsed)}";

                // Show FPS
                if (progress.CurrentFPS > 0)
                {
                    lblFPS.Text = $"Speed: {progress.CurrentFPS:F1} fps";
                }
                else
                {
                    lblFPS.Text = "Speed: --";
                }

                // Show subtitle extraction progress
                if (progress.IsExtractingSubtitles)
                {
                    lblSubtitleStatus.Text = $"Subtitle Extraction: {progress.SubtitleExtractionStatus}";
                    lblSubtitleProgress.Text = $"Subtitles: {progress.ProcessedSubtitleStreams}/{progress.TotalSubtitleStreams}";
                }
                else if (!string.IsNullOrEmpty(progress.SubtitleExtractionStatus))
                {
                    lblSubtitleStatus.Text = $"Subtitle Extraction: {progress.SubtitleExtractionStatus}";
                    lblSubtitleProgress.Text = progress.TotalSubtitleStreams > 0
                        ? $"Subtitles: {progress.ProcessedSubtitleStreams}/{progress.TotalSubtitleStreams}"
                        : "";
                }
                else
                {
                    lblSubtitleStatus.Text = "Subtitle Extraction: None";
                    lblSubtitleProgress.Text = "";
                }
            }
            else
            {
                lblStatus.Text = "Status: Idle";
                lblCurrentFile.Text = "Current File: None";
                progressBar1.Value = 0;
                lblPercent.Text = "0%";
                lblETA.Text = "Estimated Time Remaining: --";
                lblElapsed.Text = "Elapsed: --";
                lblFPS.Text = "Speed: --";

                // Reset subtitle status
                lblSubtitleStatus.Text = "Subtitle Extraction: Idle";
                lblSubtitleProgress.Text = "";
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
            }
            else if (ts.TotalMinutes >= 1)
            {
                return $"{ts.Minutes}m {ts.Seconds}s";
            }
            else
            {
                return $"{ts.Seconds}s";
            }
        }

        private void btnRefreshProgress_Click(object sender, EventArgs e)
        {
            UpdateProgress();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _progressTimer?.Stop();
            _progressTimer?.Dispose();
            base.OnFormClosing(e);
        }

        private void btnBrowseFFmpeg_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "FFmpeg Executable|ffmpeg.exe|All Files|*.*";
                dialog.Title = "Select FFmpeg Executable";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFFmpegPath.Text = dialog.FileName;
                }
            }
        }

        private void btnBrowseHandbrake_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "HandBrake CLI Executable|HandBrakeCLI.exe|All Files|*.*";
                dialog.Title = "Select HandBrakeCLI Executable";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtHandBrakePath.Text = dialog.FileName;
                }
            }
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select output directory";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtOutputDir.Text = dialog.SelectedPath;
                }
            }
        }

        private void LoadLog()
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoTranscoder");

                if (!Directory.Exists(logDir))
                {
                    txtLog.Text = "No logs directory found yet.";
                    return;
                }

                // Load list of log files
                listBoxLogFiles.Items.Clear();

                // Add current log
                string currentLog = Path.Combine(logDir, "service.log");
                if (File.Exists(currentLog))
                {
                    listBoxLogFiles.Items.Add("service.log (current)");
                }

                // Add archived logs (sorted by date, newest first)
                var archivedLogs = Directory.GetFiles(logDir, "service_*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                foreach (var log in archivedLogs)
                {
                    long sizeKB = log.Length / 1024;
                    listBoxLogFiles.Items.Add($"{log.Name} ({sizeKB:N0} KB)");
                }

                // Select current log by default
                if (listBoxLogFiles.Items.Count > 0)
                {
                    listBoxLogFiles.SelectedIndex = 0;
                    LoadSelectedLog();
                }
                else
                {
                    txtLog.Text = "No log files found yet. Service will create them when it starts processing.";
                }
            }
            catch (Exception ex)
            {
                txtLog.Text = $"Error loading logs: {ex.Message}";
            }
        }

        private void LoadSelectedLog()
        {
            try
            {
                if (listBoxLogFiles.SelectedItem == null)
                    return;

                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoTranscoder");

                string selectedItem = listBoxLogFiles.SelectedItem.ToString();
                string fileName = selectedItem.Contains("(current)")
                    ? "service.log"
                    : selectedItem.Split(' ')[0]; // Get filename before size info

                string logPath = Path.Combine(logDir, fileName);

                if (File.Exists(logPath))
                {
                    // Read last 2000 lines to avoid loading huge files
                    var lines = File.ReadAllLines(logPath);
                    var lastLines = lines.Length > 2000
                        ? lines.Skip(lines.Length - 2000)
                        : lines;

                    txtLog.Text = string.Join(Environment.NewLine, lastLines);

                    if (lines.Length > 2000)
                    {
                        txtLog.Text = $"[Showing last 2000 lines of {lines.Length} total lines]\r\n\r\n" + txtLog.Text;
                    }

                    // Scroll to bottom
                    txtLog.SelectionStart = txtLog.Text.Length;
                    txtLog.ScrollToCaret();
                }
                else
                {
                    txtLog.Text = "Log file not found.";
                }
            }
            catch (Exception ex)
            {
                txtLog.Text = $"Error loading log file: {ex.Message}";
            }
        }

        private void listBoxLogFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSelectedLog();
        }

        private void btnRefreshLog_Click(object sender, EventArgs e)
        {
            LoadLog();
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear the CURRENT log file?\n\nArchived logs will not be affected.",
                "Confirm Clear Log",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoTranscoder", "service.log");

                    if (File.Exists(logPath))
                    {
                        File.WriteAllText(logPath, "");
                        MessageBox.Show("Current log cleared successfully.", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadLog();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing log: {ex.Message}", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnOpenLogFolder_Click(object sender, EventArgs e)
        {
            try
            {
                string logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoTranscoder");

                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                Process.Start("explorer.exe", logFolder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStopService_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will stop the current transcoding operation and delete the incomplete output file.\n\n" +
                "HandBrake and FFmpeg processes will be terminated.\n\n" +
                "Are you sure you want to stop?",
                "Confirm Stop Operation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    ServiceControl.RequestStop();

                    MessageBox.Show(
                        "Stop signal sent to service.\n\n" +
                        "The current operation will be stopped within a few seconds and service will stop on next interval check.\n" +
                        "Incomplete files will be deleted.",
                        "Stop Requested",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Refresh progress immediately
                    UpdateProgress();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error sending stop signal: {ex.Message}", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private bool IsServiceRunning()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    return sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool IsServiceInstalled()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    // Just accessing the Status property will throw if service doesn't exist
                    var status = sc.Status;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void StartService()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                        MessageBox.Show("Service started successfully!", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Service is already in state: {sc.Status}", "Information",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting service: {ex.Message}\n\n" +
                               "Make sure you run this application as Administrator to control the service.",
                               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopService()
        {
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "You need to run this application as Administrator to control the service.\n\n" +
                    "Right-click the application and select 'Run as administrator'.",
                    "Administrator Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

                        MessageBox.Show("Service stopped successfully!", "Success",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Service is already in state: {sc.Status}", "Information",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping service: {ex.Message}\n\n" +
                               "Make sure you run this application as Administrator to control the service.",
                               "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetServiceStatus()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    return sc.Status.ToString();
                }
            }
            catch
            {
                return "Not Installed";
            }
        }

        private void btnStartService_Click(object sender, EventArgs e)
        {
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "You need to run this application as Administrator to control the service.\n\n" +
                    "Right-click the application and select 'Run as administrator'.",
                    "Administrator Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!IsServiceInstalled())
            {
                MessageBox.Show(
                    "Video Transcoder Service is not installed.\n\n" +
                    "Please run the installer to install the service first.",
                    "Service Not Installed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (IsServiceRunning())
            {
                MessageBox.Show(
                    "Service is already running.",
                    "Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateServiceStatus();
                return;
            }

            var result = MessageBox.Show(
                "Start the Video Transcoder Service?",
                "Confirm Start",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                StartService();
                UpdateServiceStatus();
            }
        }

        //This will stop service, but will not make sure that handbrake or ffmpeg stop.
        private void btnKillService_Click(object sender, EventArgs e)
        {
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "You need to run this application as Administrator to control the service.\n\n" +
                    "Right-click the application and select 'Run as administrator'.",
                    "Administrator Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!IsServiceRunning())
            {
                MessageBox.Show(
                    "Service is not running.",
                    "Not Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateServiceStatus();
                return;
            }

            var result = MessageBox.Show(
                "This will stop the Video Transcoder Service completely.\n\n" +
                "Any current transcoding operation may not finnish correctly.\n\n" +
                "Are you sure?",
                "Confirm Stop Service",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                StopService();
                UpdateServiceStatus();
            }
        }

        private void btnRefreshServiceStatus_Click(object sender, EventArgs e)
        {
            UpdateServiceStatus();
        }

        private void UpdateServiceStatus()
        {
            string status = GetServiceStatus();
            lblServiceStatus.Text = $"Service Status: {status}";

            bool isRunning = IsServiceRunning();
            bool isInstalled = IsServiceInstalled();

            // Enable/disable buttons based on service state
            btnStartService.Enabled = isInstalled && !isRunning;
            //btnStopServiceFull.Enabled = isRunning;
            btnStopService.Enabled = isRunning; // Your existing stop current operation button

            // Change colors based on status
            if (!isInstalled)
            {
                lblServiceStatus.ForeColor = Color.Red;
            }
            else if (isRunning)
            {
                lblServiceStatus.ForeColor = Color.Green;
            }
            else
            {
                lblServiceStatus.ForeColor = Color.Orange;
            }
        }

        private bool IsRunningAsAdmin()
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }

}
