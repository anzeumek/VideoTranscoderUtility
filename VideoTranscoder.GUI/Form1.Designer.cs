namespace VideoTranscoder.GUI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listBoxDirectories = new ListBox();
            btnAddDirectory = new Button();
            btnRemoveDirectory = new Button();
            txtOutputDir = new TextBox();
            labelOutputDir = new Label();
            txtHandBrakePath = new TextBox();
            labelHandbrakePath = new Label();
            labelChecknterval = new Label();
            numCheckInterval = new NumericUpDown();
            chkRunOnSchedule = new CheckBox();
            chkDeleteOriginal = new CheckBox();
            timeScheduleStart = new DateTimePicker();
            labelSchedule = new Label();
            btnSave = new Button();
            labelParameters = new Label();
            txtParameters = new RichTextBox();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            groupBox5 = new GroupBox();
            groupBox4 = new GroupBox();
            labelOutputFileExtension = new Label();
            txtOutputFileExtension = new TextBox();
            chkPreserveFolderStructure = new CheckBox();
            btnBrowseOutput = new Button();
            btnBrowseHandbrake = new Button();
            groupBox3 = new GroupBox();
            chkExtractSubtitles = new CheckBox();
            labelFfmpegPath = new Label();
            txtFFmpegPath = new TextBox();
            labelSubtitleFormats = new Label();
            txtSubtitleFormats = new TextBox();
            chkConvertToSrt = new CheckBox();
            btnBrowseFFmpeg = new Button();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            chkSunday = new CheckBox();
            chkSaturday = new CheckBox();
            chkFriday = new CheckBox();
            chkThursday = new CheckBox();
            chkWednesday = new CheckBox();
            chkTuesday = new CheckBox();
            chkMonday = new CheckBox();
            label1 = new Label();
            timeScheduleEnd = new DateTimePicker();
            lblServiceStatus = new Label();
            btnRefreshServiceStatus = new Button();
            btnStopServiceFull = new Button();
            btnStartService = new Button();
            btnStopService = new Button();
            tabPage2 = new TabPage();
            btnRefreshHistory = new Button();
            btnClearHistory = new Button();
            btnRemoveHistory = new Button();
            dgvHistory = new DataGridView();
            tabPage3 = new TabPage();
            btnRefreshProgress = new Button();
            progressBar1 = new ProgressBar();
            lblPercent = new Label();
            lblCurrentFile = new Label();
            lblFPS = new Label();
            lblElapsed = new Label();
            lblETA = new Label();
            lblSubtitleProgress = new Label();
            lblSubtitleStatus = new Label();
            lblStatus = new Label();
            tabPage4 = new TabPage();
            labelAvailableLogFiles = new Label();
            listBoxLogFiles = new ListBox();
            btnOpenLogFolder = new Button();
            btnClearLog = new Button();
            btnRefreshLog = new Button();
            txtLog = new RichTextBox();
            ((System.ComponentModel.ISupportInitialize)numCheckInterval).BeginInit();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            groupBox5.SuspendLayout();
            groupBox4.SuspendLayout();
            groupBox3.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvHistory).BeginInit();
            tabPage3.SuspendLayout();
            tabPage4.SuspendLayout();
            SuspendLayout();
            // 
            // listBoxDirectories
            // 
            listBoxDirectories.FormattingEnabled = true;
            listBoxDirectories.Location = new Point(179, 17);
            listBoxDirectories.Name = "listBoxDirectories";
            listBoxDirectories.Size = new Size(364, 344);
            listBoxDirectories.TabIndex = 0;
            // 
            // btnAddDirectory
            // 
            btnAddDirectory.Location = new Point(6, 25);
            btnAddDirectory.Name = "btnAddDirectory";
            btnAddDirectory.Size = new Size(167, 29);
            btnAddDirectory.TabIndex = 1;
            btnAddDirectory.Text = "Add directory";
            btnAddDirectory.UseVisualStyleBackColor = true;
            btnAddDirectory.Click += btnAddDirectory_Click;
            // 
            // btnRemoveDirectory
            // 
            btnRemoveDirectory.Location = new Point(6, 60);
            btnRemoveDirectory.Name = "btnRemoveDirectory";
            btnRemoveDirectory.Size = new Size(167, 29);
            btnRemoveDirectory.TabIndex = 2;
            btnRemoveDirectory.Text = "Remove directory";
            btnRemoveDirectory.UseVisualStyleBackColor = true;
            btnRemoveDirectory.Click += btnRemoveDirectory_Click;
            // 
            // txtOutputDir
            // 
            txtOutputDir.Location = new Point(6, 45);
            txtOutputDir.Name = "txtOutputDir";
            txtOutputDir.Size = new Size(473, 27);
            txtOutputDir.TabIndex = 3;
            // 
            // labelOutputDir
            // 
            labelOutputDir.AutoSize = true;
            labelOutputDir.Location = new Point(6, 22);
            labelOutputDir.Name = "labelOutputDir";
            labelOutputDir.Size = new Size(121, 20);
            labelOutputDir.TabIndex = 4;
            labelOutputDir.Text = "Output directory:";
            // 
            // txtHandBrakePath
            // 
            txtHandBrakePath.Location = new Point(6, 105);
            txtHandBrakePath.Name = "txtHandBrakePath";
            txtHandBrakePath.Size = new Size(473, 27);
            txtHandBrakePath.TabIndex = 3;
            // 
            // labelHandbrakePath
            // 
            labelHandbrakePath.AutoSize = true;
            labelHandbrakePath.Location = new Point(6, 82);
            labelHandbrakePath.Name = "labelHandbrakePath";
            labelHandbrakePath.Size = new Size(119, 20);
            labelHandbrakePath.TabIndex = 4;
            labelHandbrakePath.Text = "Handbrake path:";
            // 
            // labelChecknterval
            // 
            labelChecknterval.AutoSize = true;
            labelChecknterval.Location = new Point(17, 65);
            labelChecknterval.Name = "labelChecknterval";
            labelChecknterval.Size = new Size(158, 20);
            labelChecknterval.TabIndex = 4;
            labelChecknterval.Text = "Repeat scan (minutes):";
            // 
            // numCheckInterval
            // 
            numCheckInterval.Location = new Point(17, 88);
            numCheckInterval.Maximum = new decimal(new int[] { 1440, 0, 0, 0 });
            numCheckInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numCheckInterval.Name = "numCheckInterval";
            numCheckInterval.Size = new Size(150, 27);
            numCheckInterval.TabIndex = 5;
            numCheckInterval.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // chkRunOnSchedule
            // 
            chkRunOnSchedule.AutoSize = true;
            chkRunOnSchedule.Location = new Point(17, 26);
            chkRunOnSchedule.Name = "chkRunOnSchedule";
            chkRunOnSchedule.Size = new Size(139, 24);
            chkRunOnSchedule.TabIndex = 6;
            chkRunOnSchedule.Text = "Run on schedule";
            chkRunOnSchedule.UseVisualStyleBackColor = true;
            // 
            // chkDeleteOriginal
            // 
            chkDeleteOriginal.AutoSize = true;
            chkDeleteOriginal.Location = new Point(6, 95);
            chkDeleteOriginal.Name = "chkDeleteOriginal";
            chkDeleteOriginal.Size = new Size(155, 24);
            chkDeleteOriginal.TabIndex = 6;
            chkDeleteOriginal.Text = "Delete original file";
            chkDeleteOriginal.UseVisualStyleBackColor = true;
            // 
            // timeScheduleStart
            // 
            timeScheduleStart.Format = DateTimePickerFormat.Time;
            timeScheduleStart.Location = new Point(182, 88);
            timeScheduleStart.Name = "timeScheduleStart";
            timeScheduleStart.ShowUpDown = true;
            timeScheduleStart.Size = new Size(112, 27);
            timeScheduleStart.TabIndex = 7;
            // 
            // labelSchedule
            // 
            labelSchedule.AutoSize = true;
            labelSchedule.Location = new Point(182, 65);
            labelSchedule.Name = "labelSchedule";
            labelSchedule.Size = new Size(46, 20);
            labelSchedule.TabIndex = 4;
            labelSchedule.Text = "From:";
            // 
            // btnSave
            // 
            btnSave.Location = new Point(1031, 683);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(137, 29);
            btnSave.TabIndex = 8;
            btnSave.Text = "Save Settings";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // labelParameters
            // 
            labelParameters.AutoSize = true;
            labelParameters.Location = new Point(6, 157);
            labelParameters.Name = "labelParameters";
            labelParameters.Size = new Size(164, 20);
            labelParameters.TabIndex = 4;
            labelParameters.Text = "Handbrake parameters:";
            // 
            // txtParameters
            // 
            txtParameters.Location = new Point(6, 179);
            txtParameters.Name = "txtParameters";
            txtParameters.Size = new Size(597, 123);
            txtParameters.TabIndex = 9;
            txtParameters.Text = "";
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1182, 753);
            tabControl1.TabIndex = 10;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(groupBox5);
            tabPage1.Controls.Add(groupBox4);
            tabPage1.Controls.Add(groupBox3);
            tabPage1.Controls.Add(groupBox1);
            tabPage1.Controls.Add(lblServiceStatus);
            tabPage1.Controls.Add(btnRefreshServiceStatus);
            tabPage1.Controls.Add(btnStopServiceFull);
            tabPage1.Controls.Add(btnStartService);
            tabPage1.Controls.Add(btnSave);
            tabPage1.Controls.Add(btnStopService);
            tabPage1.Location = new Point(4, 29);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(1174, 720);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Settings";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // groupBox5
            // 
            groupBox5.Controls.Add(listBoxDirectories);
            groupBox5.Controls.Add(chkDeleteOriginal);
            groupBox5.Controls.Add(btnAddDirectory);
            groupBox5.Controls.Add(btnRemoveDirectory);
            groupBox5.Location = new Point(6, 7);
            groupBox5.Name = "groupBox5";
            groupBox5.Size = new Size(549, 369);
            groupBox5.TabIndex = 13;
            groupBox5.TabStop = false;
            groupBox5.Text = "Source";
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(labelOutputDir);
            groupBox4.Controls.Add(labelHandbrakePath);
            groupBox4.Controls.Add(txtHandBrakePath);
            groupBox4.Controls.Add(labelOutputFileExtension);
            groupBox4.Controls.Add(txtOutputFileExtension);
            groupBox4.Controls.Add(txtParameters);
            groupBox4.Controls.Add(txtOutputDir);
            groupBox4.Controls.Add(chkPreserveFolderStructure);
            groupBox4.Controls.Add(btnBrowseOutput);
            groupBox4.Controls.Add(labelParameters);
            groupBox4.Controls.Add(btnBrowseHandbrake);
            groupBox4.Location = new Point(561, 7);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(607, 369);
            groupBox4.TabIndex = 12;
            groupBox4.TabStop = false;
            groupBox4.Text = "Output";
            // 
            // labelOutputFileExtension
            // 
            labelOutputFileExtension.AutoSize = true;
            labelOutputFileExtension.Location = new Point(6, 310);
            labelOutputFileExtension.Name = "labelOutputFileExtension";
            labelOutputFileExtension.Size = new Size(150, 20);
            labelOutputFileExtension.TabIndex = 4;
            labelOutputFileExtension.Text = "Output file extension:";
            // 
            // txtOutputFileExtension
            // 
            txtOutputFileExtension.Location = new Point(6, 333);
            txtOutputFileExtension.Name = "txtOutputFileExtension";
            txtOutputFileExtension.Size = new Size(150, 27);
            txtOutputFileExtension.TabIndex = 3;
            txtOutputFileExtension.Text = "mp4";
            // 
            // chkPreserveFolderStructure
            // 
            chkPreserveFolderStructure.AutoSize = true;
            chkPreserveFolderStructure.Location = new Point(343, 335);
            chkPreserveFolderStructure.Name = "chkPreserveFolderStructure";
            chkPreserveFolderStructure.Size = new Size(260, 24);
            chkPreserveFolderStructure.TabIndex = 6;
            chkPreserveFolderStructure.Text = "Preserve folder structure on output";
            chkPreserveFolderStructure.UseVisualStyleBackColor = true;
            // 
            // btnBrowseOutput
            // 
            btnBrowseOutput.Location = new Point(485, 43);
            btnBrowseOutput.Name = "btnBrowseOutput";
            btnBrowseOutput.Size = new Size(118, 29);
            btnBrowseOutput.TabIndex = 8;
            btnBrowseOutput.Text = "Browse...";
            btnBrowseOutput.UseVisualStyleBackColor = true;
            btnBrowseOutput.Click += btnBrowseOutput_Click;
            // 
            // btnBrowseHandbrake
            // 
            btnBrowseHandbrake.Location = new Point(485, 103);
            btnBrowseHandbrake.Name = "btnBrowseHandbrake";
            btnBrowseHandbrake.Size = new Size(118, 29);
            btnBrowseHandbrake.TabIndex = 8;
            btnBrowseHandbrake.Text = "Browse...";
            btnBrowseHandbrake.UseVisualStyleBackColor = true;
            btnBrowseHandbrake.Click += btnBrowseHandbrake_Click;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(chkExtractSubtitles);
            groupBox3.Controls.Add(labelFfmpegPath);
            groupBox3.Controls.Add(txtFFmpegPath);
            groupBox3.Controls.Add(labelSubtitleFormats);
            groupBox3.Controls.Add(txtSubtitleFormats);
            groupBox3.Controls.Add(chkConvertToSrt);
            groupBox3.Controls.Add(btnBrowseFFmpeg);
            groupBox3.Location = new Point(6, 382);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(1162, 145);
            groupBox3.TabIndex = 11;
            groupBox3.TabStop = false;
            groupBox3.Text = "Subtitles";
            // 
            // chkExtractSubtitles
            // 
            chkExtractSubtitles.AutoSize = true;
            chkExtractSubtitles.Location = new Point(17, 26);
            chkExtractSubtitles.Name = "chkExtractSubtitles";
            chkExtractSubtitles.Size = new Size(135, 24);
            chkExtractSubtitles.TabIndex = 6;
            chkExtractSubtitles.Text = "Extract subtitles";
            chkExtractSubtitles.UseVisualStyleBackColor = true;
            // 
            // labelFfmpegPath
            // 
            labelFfmpegPath.AutoSize = true;
            labelFfmpegPath.Location = new Point(561, 30);
            labelFfmpegPath.Name = "labelFfmpegPath";
            labelFfmpegPath.Size = new Size(99, 20);
            labelFfmpegPath.TabIndex = 4;
            labelFfmpegPath.Text = "FFmpeg path:";
            // 
            // txtFFmpegPath
            // 
            txtFFmpegPath.Location = new Point(561, 53);
            txtFFmpegPath.Name = "txtFFmpegPath";
            txtFFmpegPath.Size = new Size(473, 27);
            txtFFmpegPath.TabIndex = 3;
            // 
            // labelSubtitleFormats
            // 
            labelSubtitleFormats.AutoSize = true;
            labelSubtitleFormats.Location = new Point(561, 83);
            labelSubtitleFormats.Name = "labelSubtitleFormats";
            labelSubtitleFormats.Size = new Size(256, 20);
            labelSubtitleFormats.TabIndex = 4;
            labelSubtitleFormats.Text = "Subtitle Formats (comma-separated):";
            // 
            // txtSubtitleFormats
            // 
            txtSubtitleFormats.Location = new Point(561, 106);
            txtSubtitleFormats.Name = "txtSubtitleFormats";
            txtSubtitleFormats.Size = new Size(597, 27);
            txtSubtitleFormats.TabIndex = 3;
            txtSubtitleFormats.Text = "srt, ass, vtt";
            // 
            // chkConvertToSrt
            // 
            chkConvertToSrt.AutoSize = true;
            chkConvertToSrt.Location = new Point(17, 56);
            chkConvertToSrt.Name = "chkConvertToSrt";
            chkConvertToSrt.Size = new Size(164, 24);
            chkConvertToSrt.TabIndex = 6;
            chkConvertToSrt.Text = "Auto-convert to SRT";
            chkConvertToSrt.UseVisualStyleBackColor = true;
            // 
            // btnBrowseFFmpeg
            // 
            btnBrowseFFmpeg.Location = new Point(1040, 51);
            btnBrowseFFmpeg.Name = "btnBrowseFFmpeg";
            btnBrowseFFmpeg.Size = new Size(118, 29);
            btnBrowseFFmpeg.TabIndex = 8;
            btnBrowseFFmpeg.Text = "Browse...";
            btnBrowseFFmpeg.UseVisualStyleBackColor = true;
            btnBrowseFFmpeg.Click += btnBrowseFFmpeg_Click;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(groupBox2);
            groupBox1.Controls.Add(labelSchedule);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(labelChecknterval);
            groupBox1.Controls.Add(numCheckInterval);
            groupBox1.Controls.Add(timeScheduleStart);
            groupBox1.Controls.Add(timeScheduleEnd);
            groupBox1.Controls.Add(chkRunOnSchedule);
            groupBox1.Location = new Point(6, 533);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(1162, 127);
            groupBox1.TabIndex = 10;
            groupBox1.TabStop = false;
            groupBox1.Text = "Schedule";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(chkSunday);
            groupBox2.Controls.Add(chkSaturday);
            groupBox2.Controls.Add(chkFriday);
            groupBox2.Controls.Add(chkThursday);
            groupBox2.Controls.Add(chkWednesday);
            groupBox2.Controls.Add(chkTuesday);
            groupBox2.Controls.Add(chkMonday);
            groupBox2.Location = new Point(441, 23);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(472, 93);
            groupBox2.TabIndex = 8;
            groupBox2.TabStop = false;
            groupBox2.Text = "Days to run";
            // 
            // chkSunday
            // 
            chkSunday.AutoSize = true;
            chkSunday.Location = new Point(397, 42);
            chkSunday.Name = "chkSunday";
            chkSunday.Size = new Size(55, 24);
            chkSunday.TabIndex = 0;
            chkSunday.Text = "Sun";
            chkSunday.UseVisualStyleBackColor = true;
            // 
            // chkSaturday
            // 
            chkSaturday.AutoSize = true;
            chkSaturday.Location = new Point(339, 43);
            chkSaturday.Name = "chkSaturday";
            chkSaturday.Size = new Size(52, 24);
            chkSaturday.TabIndex = 0;
            chkSaturday.Text = "Sat";
            chkSaturday.UseVisualStyleBackColor = true;
            // 
            // chkFriday
            // 
            chkFriday.AutoSize = true;
            chkFriday.Location = new Point(286, 43);
            chkFriday.Name = "chkFriday";
            chkFriday.Size = new Size(47, 24);
            chkFriday.TabIndex = 0;
            chkFriday.Text = "Fri";
            chkFriday.UseVisualStyleBackColor = true;
            // 
            // chkThursday
            // 
            chkThursday.AutoSize = true;
            chkThursday.Location = new Point(225, 43);
            chkThursday.Name = "chkThursday";
            chkThursday.Size = new Size(55, 24);
            chkThursday.TabIndex = 0;
            chkThursday.Text = "Thu";
            chkThursday.UseVisualStyleBackColor = true;
            // 
            // chkWednesday
            // 
            chkWednesday.AutoSize = true;
            chkWednesday.Location = new Point(159, 43);
            chkWednesday.Name = "chkWednesday";
            chkWednesday.Size = new Size(60, 24);
            chkWednesday.TabIndex = 0;
            chkWednesday.Text = "Wen";
            chkWednesday.UseVisualStyleBackColor = true;
            // 
            // chkTuesday
            // 
            chkTuesday.AutoSize = true;
            chkTuesday.Location = new Point(98, 43);
            chkTuesday.Name = "chkTuesday";
            chkTuesday.Size = new Size(55, 24);
            chkTuesday.TabIndex = 0;
            chkTuesday.Text = "Tue";
            chkTuesday.UseVisualStyleBackColor = true;
            // 
            // chkMonday
            // 
            chkMonday.AutoSize = true;
            chkMonday.Location = new Point(31, 42);
            chkMonday.Name = "chkMonday";
            chkMonday.Size = new Size(61, 24);
            chkMonday.TabIndex = 0;
            chkMonday.Text = "Mon";
            chkMonday.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(300, 65);
            label1.Name = "label1";
            label1.Size = new Size(28, 20);
            label1.TabIndex = 4;
            label1.Text = "To:";
            // 
            // timeScheduleEnd
            // 
            timeScheduleEnd.Format = DateTimePickerFormat.Time;
            timeScheduleEnd.Location = new Point(300, 88);
            timeScheduleEnd.Name = "timeScheduleEnd";
            timeScheduleEnd.ShowUpDown = true;
            timeScheduleEnd.Size = new Size(112, 27);
            timeScheduleEnd.TabIndex = 7;
            // 
            // lblServiceStatus
            // 
            lblServiceStatus.AutoSize = true;
            lblServiceStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 238);
            lblServiceStatus.Location = new Point(149, 687);
            lblServiceStatus.Name = "lblServiceStatus";
            lblServiceStatus.Size = new Size(182, 20);
            lblServiceStatus.TabIndex = 4;
            lblServiceStatus.Text = "Service Status: Unknown";
            // 
            // btnRefreshServiceStatus
            // 
            btnRefreshServiceStatus.Location = new Point(6, 683);
            btnRefreshServiceStatus.Name = "btnRefreshServiceStatus";
            btnRefreshServiceStatus.Size = new Size(137, 29);
            btnRefreshServiceStatus.TabIndex = 8;
            btnRefreshServiceStatus.Text = "Refresh Status";
            btnRefreshServiceStatus.UseVisualStyleBackColor = true;
            btnRefreshServiceStatus.Click += btnRefreshServiceStatus_Click;
            // 
            // btnStopServiceFull
            // 
            btnStopServiceFull.Enabled = false;
            btnStopServiceFull.Location = new Point(496, 683);
            btnStopServiceFull.Name = "btnStopServiceFull";
            btnStopServiceFull.Size = new Size(137, 29);
            btnStopServiceFull.TabIndex = 8;
            btnStopServiceFull.Text = "Kill Service";
            btnStopServiceFull.UseVisualStyleBackColor = true;
            btnStopServiceFull.Click += btnKillService_Click;
            // 
            // btnStartService
            // 
            btnStartService.Location = new Point(782, 683);
            btnStartService.Name = "btnStartService";
            btnStartService.Size = new Size(137, 29);
            btnStartService.TabIndex = 8;
            btnStartService.Text = "Start Service";
            btnStartService.UseVisualStyleBackColor = true;
            btnStartService.Click += btnStartService_Click;
            // 
            // btnStopService
            // 
            btnStopService.Location = new Point(639, 683);
            btnStopService.Name = "btnStopService";
            btnStopService.Size = new Size(137, 29);
            btnStopService.TabIndex = 2;
            btnStopService.Text = "Stop Service";
            btnStopService.UseVisualStyleBackColor = true;
            btnStopService.Click += btnStopService_Click;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(btnRefreshHistory);
            tabPage2.Controls.Add(btnClearHistory);
            tabPage2.Controls.Add(btnRemoveHistory);
            tabPage2.Controls.Add(dgvHistory);
            tabPage2.Location = new Point(4, 29);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(1174, 720);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "History";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // btnRefreshHistory
            // 
            btnRefreshHistory.Location = new Point(984, 6);
            btnRefreshHistory.Name = "btnRefreshHistory";
            btnRefreshHistory.Size = new Size(182, 29);
            btnRefreshHistory.TabIndex = 1;
            btnRefreshHistory.Text = "Refresh";
            btnRefreshHistory.UseVisualStyleBackColor = true;
            btnRefreshHistory.Click += btnRefreshHistory_Click;
            // 
            // btnClearHistory
            // 
            btnClearHistory.Location = new Point(984, 103);
            btnClearHistory.Name = "btnClearHistory";
            btnClearHistory.Size = new Size(182, 29);
            btnClearHistory.TabIndex = 1;
            btnClearHistory.Text = "Delete all";
            btnClearHistory.UseVisualStyleBackColor = true;
            btnClearHistory.Click += btnClearHistory_Click;
            // 
            // btnRemoveHistory
            // 
            btnRemoveHistory.Location = new Point(984, 68);
            btnRemoveHistory.Name = "btnRemoveHistory";
            btnRemoveHistory.Size = new Size(182, 29);
            btnRemoveHistory.TabIndex = 1;
            btnRemoveHistory.Text = "Remove selected";
            btnRemoveHistory.UseVisualStyleBackColor = true;
            btnRemoveHistory.Click += btnRemoveHistory_Click;
            // 
            // dgvHistory
            // 
            dgvHistory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvHistory.Location = new Point(6, 6);
            dgvHistory.MultiSelect = false;
            dgvHistory.Name = "dgvHistory";
            dgvHistory.ReadOnly = true;
            dgvHistory.RowHeadersWidth = 51;
            dgvHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvHistory.Size = new Size(972, 706);
            dgvHistory.TabIndex = 0;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(btnRefreshProgress);
            tabPage3.Controls.Add(progressBar1);
            tabPage3.Controls.Add(lblPercent);
            tabPage3.Controls.Add(lblCurrentFile);
            tabPage3.Controls.Add(lblFPS);
            tabPage3.Controls.Add(lblElapsed);
            tabPage3.Controls.Add(lblETA);
            tabPage3.Controls.Add(lblSubtitleProgress);
            tabPage3.Controls.Add(lblSubtitleStatus);
            tabPage3.Controls.Add(lblStatus);
            tabPage3.Location = new Point(4, 29);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(1174, 720);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Progress";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // btnRefreshProgress
            // 
            btnRefreshProgress.Location = new Point(1072, 9);
            btnRefreshProgress.Name = "btnRefreshProgress";
            btnRefreshProgress.Size = new Size(94, 29);
            btnRefreshProgress.TabIndex = 2;
            btnRefreshProgress.Text = "Refresh";
            btnRefreshProgress.UseVisualStyleBackColor = true;
            btnRefreshProgress.Click += btnRefreshProgress_Click;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(8, 683);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(1158, 29);
            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.TabIndex = 1;
            // 
            // lblPercent
            // 
            lblPercent.AutoSize = true;
            lblPercent.Location = new Point(8, 660);
            lblPercent.Name = "lblPercent";
            lblPercent.Size = new Size(29, 20);
            lblPercent.TabIndex = 0;
            lblPercent.Text = "0%";
            // 
            // lblCurrentFile
            // 
            lblCurrentFile.AutoSize = true;
            lblCurrentFile.Location = new Point(17, 58);
            lblCurrentFile.Name = "lblCurrentFile";
            lblCurrentFile.Size = new Size(127, 20);
            lblCurrentFile.TabIndex = 0;
            lblCurrentFile.Text = "Current File: None";
            // 
            // lblFPS
            // 
            lblFPS.AutoSize = true;
            lblFPS.Location = new Point(17, 178);
            lblFPS.Name = "lblFPS";
            lblFPS.Size = new Size(94, 20);
            lblFPS.TabIndex = 0;
            lblFPS.Text = "Speed: -- fps";
            // 
            // lblElapsed
            // 
            lblElapsed.AutoSize = true;
            lblElapsed.Location = new Point(17, 98);
            lblElapsed.Name = "lblElapsed";
            lblElapsed.Size = new Size(80, 20);
            lblElapsed.TabIndex = 0;
            lblElapsed.Text = "Elapsed: --";
            // 
            // lblETA
            // 
            lblETA.AutoSize = true;
            lblETA.Location = new Point(17, 138);
            lblETA.Name = "lblETA";
            lblETA.Size = new Size(206, 20);
            lblETA.TabIndex = 0;
            lblETA.Text = "Estimated Time Remaining: --";
            // 
            // lblSubtitleProgress
            // 
            lblSubtitleProgress.AutoSize = true;
            lblSubtitleProgress.Location = new Point(17, 318);
            lblSubtitleProgress.Name = "lblSubtitleProgress";
            lblSubtitleProgress.Size = new Size(95, 20);
            lblSubtitleProgress.TabIndex = 0;
            lblSubtitleProgress.Text = "Subtitles: 0/0";
            // 
            // lblSubtitleStatus
            // 
            lblSubtitleStatus.AutoSize = true;
            lblSubtitleStatus.Location = new Point(17, 278);
            lblSubtitleStatus.Name = "lblSubtitleStatus";
            lblSubtitleStatus.Size = new Size(162, 20);
            lblSubtitleStatus.TabIndex = 0;
            lblSubtitleStatus.Text = "Subtitle Extraction: Idle";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(17, 18);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(81, 20);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Status: Idle";
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(labelAvailableLogFiles);
            tabPage4.Controls.Add(listBoxLogFiles);
            tabPage4.Controls.Add(btnOpenLogFolder);
            tabPage4.Controls.Add(btnClearLog);
            tabPage4.Controls.Add(btnRefreshLog);
            tabPage4.Controls.Add(txtLog);
            tabPage4.Location = new Point(4, 29);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(3);
            tabPage4.Size = new Size(1174, 720);
            tabPage4.TabIndex = 3;
            tabPage4.Text = "Logs";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // labelAvailableLogFiles
            // 
            labelAvailableLogFiles.AutoSize = true;
            labelAvailableLogFiles.Location = new Point(163, 10);
            labelAvailableLogFiles.Name = "labelAvailableLogFiles";
            labelAvailableLogFiles.Size = new Size(136, 20);
            labelAvailableLogFiles.TabIndex = 3;
            labelAvailableLogFiles.Text = "Available Log Files:";
            // 
            // listBoxLogFiles
            // 
            listBoxLogFiles.FormattingEnabled = true;
            listBoxLogFiles.Location = new Point(305, 6);
            listBoxLogFiles.Name = "listBoxLogFiles";
            listBoxLogFiles.Size = new Size(866, 104);
            listBoxLogFiles.TabIndex = 2;
            listBoxLogFiles.SelectedIndexChanged += listBoxLogFiles_SelectedIndexChanged;
            // 
            // btnOpenLogFolder
            // 
            btnOpenLogFolder.Location = new Point(3, 81);
            btnOpenLogFolder.Name = "btnOpenLogFolder";
            btnOpenLogFolder.Size = new Size(154, 29);
            btnOpenLogFolder.TabIndex = 1;
            btnOpenLogFolder.Text = "Open Log Folder";
            btnOpenLogFolder.UseVisualStyleBackColor = true;
            btnOpenLogFolder.Click += btnOpenLogFolder_Click;
            // 
            // btnClearLog
            // 
            btnClearLog.Location = new Point(3, 41);
            btnClearLog.Name = "btnClearLog";
            btnClearLog.Size = new Size(94, 29);
            btnClearLog.TabIndex = 1;
            btnClearLog.Text = "Clear";
            btnClearLog.UseVisualStyleBackColor = true;
            btnClearLog.Click += btnClearLog_Click;
            // 
            // btnRefreshLog
            // 
            btnRefreshLog.Location = new Point(3, 6);
            btnRefreshLog.Name = "btnRefreshLog";
            btnRefreshLog.Size = new Size(94, 29);
            btnRefreshLog.TabIndex = 1;
            btnRefreshLog.Text = "Refresh";
            btnRefreshLog.UseVisualStyleBackColor = true;
            btnRefreshLog.Click += btnRefreshLog_Click;
            // 
            // txtLog
            // 
            txtLog.Location = new Point(3, 116);
            txtLog.Name = "txtLog";
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtLog.Size = new Size(1168, 601);
            txtLog.TabIndex = 0;
            txtLog.Text = "";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1182, 753);
            Controls.Add(tabControl1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)numCheckInterval).EndInit();
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            groupBox5.ResumeLayout(false);
            groupBox5.PerformLayout();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvHistory).EndInit();
            tabPage3.ResumeLayout(false);
            tabPage3.PerformLayout();
            tabPage4.ResumeLayout(false);
            tabPage4.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private ListBox listBoxDirectories;
        private Button btnAddDirectory;
        private Button btnRemoveDirectory;
        private TextBox txtOutputDir;
        private Label labelOutputDir;
        private TextBox txtHandBrakePath;
        private Label labelHandbrakePath;
        private Label labelChecknterval;
        private NumericUpDown numCheckInterval;
        private CheckBox chkRunOnSchedule;
        private CheckBox chkDeleteOriginal;
        private DateTimePicker timeScheduleStart;
        private Label labelSchedule;
        private Button btnSave;
        private Label labelParameters;
        private RichTextBox txtParameters;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private Button btnRemoveHistory;
        private DataGridView dgvHistory;
        private Button btnRefreshHistory;
        private Button btnClearHistory;
        private TabPage tabPage3;
        private Button btnRefreshProgress;
        private ProgressBar progressBar1;
        private Label lblPercent;
        private Label lblCurrentFile;
        private Label lblStatus;
        private Label lblFPS;
        private Label lblElapsed;
        private Label lblETA;
        private DateTimePicker timeScheduleEnd;
        private Label label1;
        private GroupBox groupBox1;
        private GroupBox groupBox2;
        private CheckBox chkSunday;
        private CheckBox chkSaturday;
        private CheckBox chkFriday;
        private CheckBox chkThursday;
        private CheckBox chkWednesday;
        private CheckBox chkTuesday;
        private CheckBox chkMonday;
        private CheckBox chkExtractSubtitles;
        private Button btnBrowseFFmpeg;
        private TextBox txtSubtitleFormats;
        private Label labelSubtitleFormats;
        private TextBox txtFFmpegPath;
        private Label labelFfmpegPath;
        private Button btnBrowseHandbrake;
        private Button btnBrowseOutput;
        private CheckBox chkConvertToSrt;
        private Label lblSubtitleProgress;
        private Label lblSubtitleStatus;
        private TabPage tabPage4;
        private Button btnOpenLogFolder;
        private Button btnClearLog;
        private Button btnRefreshLog;
        private RichTextBox txtLog;
        private ListBox listBoxLogFiles;
        private Label labelAvailableLogFiles;
        private Button btnStopService;
        private Label lblServiceStatus;
        private Button btnRefreshServiceStatus;
        private Button btnStopServiceFull;
        private Button btnStartService;
        private CheckBox chkPreserveFolderStructure;
        private TextBox txtOutputFileExtension;
        private Label labelOutputFileExtension;
        private GroupBox groupBox3;
        private GroupBox groupBox4;
        private GroupBox groupBox5;
    }
}
