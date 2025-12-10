namespace TorrentClient
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                
                _updateTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            _torrentListView = new ListView();
            colNumber = new ColumnHeader();
            colName = new ColumnHeader();
            colSize = new ColumnHeader();
            colProgress = new ColumnHeader();
            colSpeed = new ColumnHeader();
            colDownloaded = new ColumnHeader();
            colPeers = new ColumnHeader();
            colPriority = new ColumnHeader();
            colStatus = new ColumnHeader();
            _toolbarPanel = new Panel();
            _addTorrentButton = new Button();
            _removeTorrentButton = new Button();
            _separator1 = new Panel();
            _startButton = new Button();
            _pauseButton = new Button();
            _stopButton = new Button();
            _separator2 = new Panel();
            _settingsButton = new Button();
            _selectDownloadFolderButton = new Button();
            _globalSettingsButton = new Button();
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel();
            _statusSeparator = new ToolStripSeparator();
            _downloadSpeedLabel = new ToolStripStatusLabel();
            _uploadSpeedLabel = new ToolStripStatusLabel();
            _updateTimer = new System.Windows.Forms.Timer(components);
            _toolbarPanel.SuspendLayout();
            _statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // _torrentListView
            // 
            _torrentListView.Columns.AddRange(new ColumnHeader[] { colNumber, colName, colSize, colProgress, colSpeed, colDownloaded, colPeers, colPriority, colStatus });
            _torrentListView.Dock = DockStyle.Fill;
            _torrentListView.FullRowSelect = true;
            _torrentListView.GridLines = true;
            _torrentListView.Location = new Point(0, 88);
            _torrentListView.Margin = new Padding(5);
            _torrentListView.Name = "_torrentListView";
            _torrentListView.ShowItemToolTips = true;
            _torrentListView.Size = new Size(1643, 681);
            _torrentListView.TabIndex = 0;
            _torrentListView.UseCompatibleStateImageBehavior = false;
            _torrentListView.View = View.Details;
            _torrentListView.ColumnWidthChanging += TorrentListView_ColumnWidthChanging;
            _torrentListView.SelectedIndexChanged += TorrentListView_SelectedIndexChanged;
            _torrentListView.KeyDown += TorrentListView_KeyDown;
            _torrentListView.MouseClick += TorrentListView_MouseClick;
            // 
            // colNumber
            // 
            colNumber.Text = "№";
            colNumber.Width = 40;
            // 
            // colName
            // 
            colName.Text = "Название";
            colName.Width = 460;
            // 
            // colSize
            // 
            colSize.Text = "Размер";
            colSize.Width = 200;
            // 
            // colProgress
            // 
            colProgress.Text = "Прогресс";
            colProgress.Width = 120;
            // 
            // colSpeed
            // 
            colSpeed.Text = "Скорость";
            colSpeed.Width = 275;
            // 
            // colDownloaded
            // 
            colDownloaded.Text = "Загружено";
            colDownloaded.Width = 170;
            // 
            // colPeers
            // 
            colPeers.Text = "Пиры";
            colPeers.Width = 130;
            // 
            // colPriority
            // 
            colPriority.Text = "Приоритет";
            colPriority.Width = 120;
            // 
            // colStatus
            // 
            colStatus.Text = "Статус";
            colStatus.Width = 140;
            // 
            // _toolbarPanel
            // 
            _toolbarPanel.BackColor = SystemColors.ControlLight;
            _toolbarPanel.Controls.Add(_addTorrentButton);
            _toolbarPanel.Controls.Add(_removeTorrentButton);
            _toolbarPanel.Controls.Add(_separator1);
            _toolbarPanel.Controls.Add(_startButton);
            _toolbarPanel.Controls.Add(_pauseButton);
            _toolbarPanel.Controls.Add(_stopButton);
            _toolbarPanel.Controls.Add(_separator2);
            _toolbarPanel.Controls.Add(_settingsButton);
            _toolbarPanel.Controls.Add(_selectDownloadFolderButton);
            _toolbarPanel.Controls.Add(_globalSettingsButton);
            _toolbarPanel.Dock = DockStyle.Top;
            _toolbarPanel.Location = new Point(0, 0);
            _toolbarPanel.Margin = new Padding(5);
            _toolbarPanel.Name = "_toolbarPanel";
            _toolbarPanel.Size = new Size(1643, 88);
            _toolbarPanel.TabIndex = 1;
            // 
            // _addTorrentButton
            // 
            _addTorrentButton.Location = new Point(18, 18);
            _addTorrentButton.Margin = new Padding(5);
            _addTorrentButton.Name = "_addTorrentButton";
            _addTorrentButton.Size = new Size(210, 52);
            _addTorrentButton.TabIndex = 0;
            _addTorrentButton.Text = "Добавить торрент";
            _addTorrentButton.UseVisualStyleBackColor = true;
            _addTorrentButton.Click += AddTorrentButton_Click;
            // 
            // _removeTorrentButton
            // 
            _removeTorrentButton.Enabled = false;
            _removeTorrentButton.Location = new Point(245, 18);
            _removeTorrentButton.Margin = new Padding(5);
            _removeTorrentButton.Name = "_removeTorrentButton";
            _removeTorrentButton.Size = new Size(140, 52);
            _removeTorrentButton.TabIndex = 1;
            _removeTorrentButton.Text = "Удалить";
            _removeTorrentButton.UseVisualStyleBackColor = true;
            _removeTorrentButton.Click += RemoveTorrentButton_Click;
            // 
            // _separator1
            // 
            _separator1.BackColor = SystemColors.ControlDark;
            _separator1.Location = new Point(393, 18);
            _separator1.Name = "_separator1";
            _separator1.Size = new Size(2, 52);
            _separator1.TabIndex = 8;
            // 
            // _startButton
            // 
            _startButton.Enabled = false;
            _startButton.Location = new Point(407, 18);
            _startButton.Margin = new Padding(5);
            _startButton.Name = "_startButton";
            _startButton.Size = new Size(105, 52);
            _startButton.TabIndex = 2;
            _startButton.Text = "Старт";
            _startButton.UseVisualStyleBackColor = true;
            _startButton.Click += StartButton_Click;
            // 
            // _pauseButton
            // 
            _pauseButton.Enabled = false;
            _pauseButton.Location = new Point(520, 18);
            _pauseButton.Margin = new Padding(5);
            _pauseButton.Name = "_pauseButton";
            _pauseButton.Size = new Size(105, 52);
            _pauseButton.TabIndex = 3;
            _pauseButton.Text = "Пауза";
            _pauseButton.UseVisualStyleBackColor = true;
            _pauseButton.Click += PauseButton_Click;
            // 
            // _stopButton
            // 
            _stopButton.Enabled = false;
            _stopButton.Location = new Point(633, 18);
            _stopButton.Margin = new Padding(5);
            _stopButton.Name = "_stopButton";
            _stopButton.Size = new Size(105, 52);
            _stopButton.TabIndex = 4;
            _stopButton.Text = "Стоп";
            _stopButton.UseVisualStyleBackColor = true;
            _stopButton.Click += StopButton_Click;
            // 
            // _separator2
            // 
            _separator2.BackColor = SystemColors.ControlDark;
            _separator2.Location = new Point(746, 18);
            _separator2.Name = "_separator2";
            _separator2.Size = new Size(2, 52);
            _separator2.TabIndex = 9;
            // 
            // _settingsButton
            // 
            _settingsButton.Enabled = false;
            _settingsButton.Location = new Point(1030, 18);
            _settingsButton.Margin = new Padding(5);
            _settingsButton.Name = "_settingsButton";
            _settingsButton.Size = new Size(262, 52);
            _settingsButton.TabIndex = 5;
            _settingsButton.Text = "Ограничение скорости";
            _settingsButton.UseVisualStyleBackColor = true;
            _settingsButton.Click += SettingsButton_Click;
            // 
            // _selectDownloadFolderButton
            // 
            _selectDownloadFolderButton.Location = new Point(760, 18);
            _selectDownloadFolderButton.Margin = new Padding(5);
            _selectDownloadFolderButton.Name = "_selectDownloadFolderButton";
            _selectDownloadFolderButton.Size = new Size(262, 52);
            _selectDownloadFolderButton.TabIndex = 6;
            _selectDownloadFolderButton.Text = "Папка сохранения";
            _selectDownloadFolderButton.UseVisualStyleBackColor = true;
            _selectDownloadFolderButton.Click += SelectDownloadFolderButton_Click;
            // 
            // _globalSettingsButton
            // 
            _globalSettingsButton.Location = new Point(1300, 18);
            _globalSettingsButton.Margin = new Padding(5);
            _globalSettingsButton.Name = "_globalSettingsButton";
            _globalSettingsButton.Size = new Size(228, 52);
            _globalSettingsButton.TabIndex = 7;
            _globalSettingsButton.Text = "Настройки";
            _globalSettingsButton.UseVisualStyleBackColor = true;
            _globalSettingsButton.Click += GlobalSettingsButton_Click;
            // 
            // _statusStrip
            // 
            _statusStrip.ImageScalingSize = new Size(28, 28);
            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _statusSeparator, _downloadSpeedLabel, _uploadSpeedLabel });
            _statusStrip.Location = new Point(0, 769);
            _statusStrip.Name = "_statusStrip";
            _statusStrip.Padding = new Padding(2, 0, 24, 0);
            _statusStrip.Size = new Size(1643, 39);
            _statusStrip.TabIndex = 2;
            _statusStrip.Text = "statusStrip1";
            // 
            // _statusLabel
            // 
            _statusLabel.Name = "_statusLabel";
            _statusLabel.Size = new Size(1415, 30);
            _statusLabel.Spring = true;
            _statusLabel.Text = "Готов";
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _statusSeparator
            // 
            _statusSeparator.Name = "_statusSeparator";
            _statusSeparator.Size = new Size(6, 39);
            // 
            // _downloadSpeedLabel
            // 
            _downloadSpeedLabel.ForeColor = Color.Green;
            _downloadSpeedLabel.Name = "_downloadSpeedLabel";
            _downloadSpeedLabel.Size = new Size(98, 30);
            _downloadSpeedLabel.Text = "↓ 0 Mbps";
            // 
            // _uploadSpeedLabel
            // 
            _uploadSpeedLabel.ForeColor = Color.Blue;
            _uploadSpeedLabel.Name = "_uploadSpeedLabel";
            _uploadSpeedLabel.Size = new Size(98, 30);
            _uploadSpeedLabel.Text = "↑ 0 Mbps";
            // 
            // _updateTimer
            // 
            _updateTimer.Interval = 2000;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(168F, 168F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1643, 808);
            Controls.Add(_torrentListView);
            Controls.Add(_toolbarPanel);
            Controls.Add(_statusStrip);
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Margin = new Padding(5);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = " Torrent Client";
            _toolbarPanel.ResumeLayout(false);
            _statusStrip.ResumeLayout(false);
            _statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView _torrentListView;
        private System.Windows.Forms.ColumnHeader colNumber;
        private System.Windows.Forms.ColumnHeader colName;
        private System.Windows.Forms.ColumnHeader colSize;
        private System.Windows.Forms.ColumnHeader colProgress;
        private System.Windows.Forms.ColumnHeader colSpeed;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.ColumnHeader colPeers;
        private System.Windows.Forms.ColumnHeader colPriority;
        private System.Windows.Forms.ColumnHeader colDownloaded;
        private System.Windows.Forms.Panel _toolbarPanel;
        private System.Windows.Forms.Button _addTorrentButton;
        private System.Windows.Forms.Button _removeTorrentButton;
        private System.Windows.Forms.Button _startButton;
        private System.Windows.Forms.Button _pauseButton;
        private System.Windows.Forms.Button _stopButton;
        private System.Windows.Forms.Button _settingsButton;
        private System.Windows.Forms.Button _selectDownloadFolderButton;
        private System.Windows.Forms.Button _globalSettingsButton;
        private System.Windows.Forms.Panel _separator1;
        private System.Windows.Forms.Panel _separator2;
        private System.Windows.Forms.StatusStrip _statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel _statusLabel;
        private System.Windows.Forms.ToolStripSeparator _statusSeparator;
        private System.Windows.Forms.ToolStripStatusLabel _downloadSpeedLabel;
        private System.Windows.Forms.ToolStripStatusLabel _uploadSpeedLabel;
        private System.Windows.Forms.Timer _updateTimer;
    }
}

