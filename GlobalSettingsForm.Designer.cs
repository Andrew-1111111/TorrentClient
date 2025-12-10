namespace TorrentClient
{
    partial class GlobalSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GlobalSettingsForm));
            _maxConnectionsLabel = new Label();
            _maxConnectionsNumeric = new NumericUpDown();
            _maxHalfOpenLabel = new Label();
            _maxHalfOpenNumeric = new NumericUpDown();
            _maxPiecesLabel = new Label();
            _maxPiecesNumeric = new NumericUpDown();
            _maxRequestsLabel = new Label();
            _maxRequestsNumeric = new NumericUpDown();
            _globalDownloadLimitCheckBox = new CheckBox();
            _globalDownloadLimitComboBox = new ComboBox();
            _globalUploadLimitCheckBox = new CheckBox();
            _globalUploadLimitComboBox = new ComboBox();
            _enableLoggingCheckBox = new CheckBox();
            _minimizeToTrayCheckBox = new CheckBox();
            _autoStartCheckBox = new CheckBox();
            _copyTorrentFileCheckBox = new CheckBox();
            _autoStartOnAddCheckBox = new CheckBox();
            _languageLabel = new Label();
            _languageComboBox = new ComboBox();
            _okButton = new Button();
            _cancelButton = new Button();
            _infoLabel = new Label();
            ((System.ComponentModel.ISupportInitialize)_maxConnectionsNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_maxHalfOpenNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_maxPiecesNumeric).BeginInit();
            ((System.ComponentModel.ISupportInitialize)_maxRequestsNumeric).BeginInit();
            SuspendLayout();
            // 
            // _maxConnectionsLabel
            // 
            _maxConnectionsLabel.AutoSize = true;
            _maxConnectionsLabel.Location = new Point(35, 35);
            _maxConnectionsLabel.Margin = new Padding(5, 0, 5, 0);
            _maxConnectionsLabel.Name = "_maxConnectionsLabel";
            _maxConnectionsLabel.Size = new Size(290, 30);
            _maxConnectionsLabel.TabIndex = 0;
            _maxConnectionsLabel.Text = "Макс. соединений с пирами:";
            // 
            // _maxConnectionsNumeric
            // 
            _maxConnectionsNumeric.Location = new Point(472, 32);
            _maxConnectionsNumeric.Margin = new Padding(5);
            _maxConnectionsNumeric.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
            _maxConnectionsNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _maxConnectionsNumeric.Name = "_maxConnectionsNumeric";
            _maxConnectionsNumeric.Size = new Size(140, 35);
            _maxConnectionsNumeric.TabIndex = 1;
            _maxConnectionsNumeric.Value = new decimal(new int[] { 200, 0, 0, 0 });
            // 
            // _maxHalfOpenLabel
            // 
            _maxHalfOpenLabel.AutoSize = true;
            _maxHalfOpenLabel.Location = new Point(35, 96);
            _maxHalfOpenLabel.Margin = new Padding(5, 0, 5, 0);
            _maxHalfOpenLabel.Name = "_maxHalfOpenLabel";
            _maxHalfOpenLabel.Size = new Size(337, 30);
            _maxHalfOpenLabel.TabIndex = 2;
            _maxHalfOpenLabel.Text = "Макс. полуоткрытых соединений:";
            // 
            // _maxHalfOpenNumeric
            // 
            _maxHalfOpenNumeric.Location = new Point(472, 93);
            _maxHalfOpenNumeric.Margin = new Padding(5);
            _maxHalfOpenNumeric.Maximum = new decimal(new int[] { 2000, 0, 0, 0 });
            _maxHalfOpenNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _maxHalfOpenNumeric.Name = "_maxHalfOpenNumeric";
            _maxHalfOpenNumeric.Size = new Size(140, 35);
            _maxHalfOpenNumeric.TabIndex = 3;
            _maxHalfOpenNumeric.Value = new decimal(new int[] { 50, 0, 0, 0 });
            // 
            // _maxPiecesLabel
            // 
            _maxPiecesLabel.AutoSize = true;
            _maxPiecesLabel.Location = new Point(35, 158);
            _maxPiecesLabel.Margin = new Padding(5, 0, 5, 0);
            _maxPiecesLabel.Name = "_maxPiecesLabel";
            _maxPiecesLabel.Size = new Size(232, 30);
            _maxPiecesLabel.TabIndex = 4;
            _maxPiecesLabel.Text = "Кусков одновременно:";
            // 
            // _maxPiecesNumeric
            // 
            _maxPiecesNumeric.Location = new Point(472, 154);
            _maxPiecesNumeric.Margin = new Padding(5);
            _maxPiecesNumeric.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            _maxPiecesNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _maxPiecesNumeric.Name = "_maxPiecesNumeric";
            _maxPiecesNumeric.Size = new Size(140, 35);
            _maxPiecesNumeric.TabIndex = 5;
            _maxPiecesNumeric.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // _maxRequestsLabel
            // 
            _maxRequestsLabel.AutoSize = true;
            _maxRequestsLabel.Location = new Point(35, 219);
            _maxRequestsLabel.Margin = new Padding(5, 0, 5, 0);
            _maxRequestsLabel.Name = "_maxRequestsLabel";
            _maxRequestsLabel.Size = new Size(258, 30);
            _maxRequestsLabel.TabIndex = 6;
            _maxRequestsLabel.Text = "Запросов на соединение:";
            // 
            // _maxRequestsNumeric
            // 
            _maxRequestsNumeric.Location = new Point(472, 215);
            _maxRequestsNumeric.Margin = new Padding(5);
            _maxRequestsNumeric.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            _maxRequestsNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            _maxRequestsNumeric.Name = "_maxRequestsNumeric";
            _maxRequestsNumeric.Size = new Size(140, 35);
            _maxRequestsNumeric.TabIndex = 7;
            _maxRequestsNumeric.Value = new decimal(new int[] { 16, 0, 0, 0 });
            // 
            // _globalDownloadLimitCheckBox
            // 
            _globalDownloadLimitCheckBox.AutoSize = true;
            _globalDownloadLimitCheckBox.Location = new Point(35, 280);
            _globalDownloadLimitCheckBox.Margin = new Padding(5);
            _globalDownloadLimitCheckBox.Name = "_globalDownloadLimitCheckBox";
            _globalDownloadLimitCheckBox.Size = new Size(360, 34);
            _globalDownloadLimitCheckBox.TabIndex = 8;
            _globalDownloadLimitCheckBox.Text = "Общая скорость загрузки (Mbps):";
            _globalDownloadLimitCheckBox.UseVisualStyleBackColor = true;
            // 
            // _globalDownloadLimitComboBox
            // 
            _globalDownloadLimitComboBox.FormattingEnabled = true;
            _globalDownloadLimitComboBox.Items.AddRange(new object[] { "1", "2", "5", "10", "20", "50", "100", "200", "500", "1000" });
            _globalDownloadLimitComboBox.Location = new Point(472, 276);
            _globalDownloadLimitComboBox.Margin = new Padding(5);
            _globalDownloadLimitComboBox.Name = "_globalDownloadLimitComboBox";
            _globalDownloadLimitComboBox.Size = new Size(137, 38);
            _globalDownloadLimitComboBox.TabIndex = 9;
            _globalDownloadLimitComboBox.Text = "100";
            // 
            // _globalUploadLimitCheckBox
            // 
            _globalUploadLimitCheckBox.AutoSize = true;
            _globalUploadLimitCheckBox.Location = new Point(35, 332);
            _globalUploadLimitCheckBox.Margin = new Padding(5);
            _globalUploadLimitCheckBox.Name = "_globalUploadLimitCheckBox";
            _globalUploadLimitCheckBox.Size = new Size(346, 34);
            _globalUploadLimitCheckBox.TabIndex = 10;
            _globalUploadLimitCheckBox.Text = "Общая скорость отдачи (Mbps):";
            _globalUploadLimitCheckBox.UseVisualStyleBackColor = true;
            // 
            // _globalUploadLimitComboBox
            // 
            _globalUploadLimitComboBox.FormattingEnabled = true;
            _globalUploadLimitComboBox.Items.AddRange(new object[] { "1", "2", "5", "10", "20", "50", "100", "200", "500", "1000" });
            _globalUploadLimitComboBox.Location = new Point(472, 329);
            _globalUploadLimitComboBox.Margin = new Padding(5);
            _globalUploadLimitComboBox.Name = "_globalUploadLimitComboBox";
            _globalUploadLimitComboBox.Size = new Size(137, 38);
            _globalUploadLimitComboBox.TabIndex = 11;
            _globalUploadLimitComboBox.Text = "100";
            // 
            // _enableLoggingCheckBox
            // 
            _enableLoggingCheckBox.AutoSize = true;
            _enableLoggingCheckBox.Location = new Point(35, 385);
            _enableLoggingCheckBox.Margin = new Padding(5);
            _enableLoggingCheckBox.Name = "_enableLoggingCheckBox";
            _enableLoggingCheckBox.Size = new Size(263, 34);
            _enableLoggingCheckBox.TabIndex = 12;
            _enableLoggingCheckBox.Text = "Включить логирование";
            _enableLoggingCheckBox.UseVisualStyleBackColor = true;
            // 
            // _minimizeToTrayCheckBox
            // 
            _minimizeToTrayCheckBox.AutoSize = true;
            _minimizeToTrayCheckBox.Location = new Point(35, 429);
            _minimizeToTrayCheckBox.Margin = new Padding(5);
            _minimizeToTrayCheckBox.Name = "_minimizeToTrayCheckBox";
            _minimizeToTrayCheckBox.Size = new Size(420, 34);
            _minimizeToTrayCheckBox.TabIndex = 13;
            _minimizeToTrayCheckBox.Text = "Сворачивать в трей при закрытии окна";
            _minimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            // 
            // _autoStartCheckBox
            // 
            _autoStartCheckBox.AutoSize = true;
            _autoStartCheckBox.Location = new Point(35, 472);
            _autoStartCheckBox.Margin = new Padding(5);
            _autoStartCheckBox.Name = "_autoStartCheckBox";
            _autoStartCheckBox.Size = new Size(519, 34);
            _autoStartCheckBox.TabIndex = 14;
            _autoStartCheckBox.Text = "Автозапуск торрентов при открытии приложения";
            _autoStartCheckBox.UseVisualStyleBackColor = true;
            // 
            // _copyTorrentFileCheckBox
            // 
            _copyTorrentFileCheckBox.AutoSize = true;
            _copyTorrentFileCheckBox.Location = new Point(35, 516);
            _copyTorrentFileCheckBox.Margin = new Padding(5);
            _copyTorrentFileCheckBox.Name = "_copyTorrentFileCheckBox";
            _copyTorrentFileCheckBox.Size = new Size(446, 34);
            _copyTorrentFileCheckBox.TabIndex = 15;
            _copyTorrentFileCheckBox.Text = "Копировать .torrent файл в папку загрузки";
            _copyTorrentFileCheckBox.UseVisualStyleBackColor = true;
            // 
            // _autoStartOnAddCheckBox
            // 
            _autoStartOnAddCheckBox.AutoSize = true;
            _autoStartOnAddCheckBox.Location = new Point(35, 560);
            _autoStartOnAddCheckBox.Margin = new Padding(5);
            _autoStartOnAddCheckBox.Name = "_autoStartOnAddCheckBox";
            _autoStartOnAddCheckBox.Size = new Size(532, 34);
            _autoStartOnAddCheckBox.TabIndex = 16;
            _autoStartOnAddCheckBox.Text = "Автоматически запускать торрент при добавлении";
            _autoStartOnAddCheckBox.UseVisualStyleBackColor = true;
            // 
            // _languageLabel
            // 
            _languageLabel.AutoSize = true;
            _languageLabel.Location = new Point(35, 607);
            _languageLabel.Margin = new Padding(5, 0, 5, 0);
            _languageLabel.Name = "_languageLabel";
            _languageLabel.Size = new Size(64, 30);
            _languageLabel.TabIndex = 20;
            _languageLabel.Text = "Язык:";
            // 
            // _languageComboBox
            // 
            _languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _languageComboBox.FormattingEnabled = true;
            _languageComboBox.Location = new Point(309, 604);
            _languageComboBox.Margin = new Padding(5);
            _languageComboBox.Name = "_languageComboBox";
            _languageComboBox.Size = new Size(300, 38);
            _languageComboBox.TabIndex = 21;
            // 
            // _okButton
            // 
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Location = new Point(271, 750);
            _okButton.Margin = new Padding(5);
            _okButton.Name = "_okButton";
            _okButton.Size = new Size(158, 52);
            _okButton.TabIndex = 18;
            _okButton.Text = "Сохранить";
            _okButton.UseVisualStyleBackColor = true;
            // 
            // _cancelButton
            // 
            _cancelButton.DialogResult = DialogResult.Cancel;
            _cancelButton.Location = new Point(446, 750);
            _cancelButton.Margin = new Padding(5);
            _cancelButton.Name = "_cancelButton";
            _cancelButton.Size = new Size(158, 52);
            _cancelButton.TabIndex = 19;
            _cancelButton.Text = "Отмена";
            _cancelButton.UseVisualStyleBackColor = true;
            // 
            // _infoLabel
            // 
            _infoLabel.ForeColor = Color.Gray;
            _infoLabel.Location = new Point(35, 670);
            _infoLabel.Margin = new Padding(5, 0, 5, 0);
            _infoLabel.Name = "_infoLabel";
            _infoLabel.Size = new Size(578, 61);
            _infoLabel.TabIndex = 17;
            _infoLabel.Text = "Увеличение параллелизма может повысить скорость, но создаёт больше нагрузки на систему.";
            // 
            // GlobalSettingsForm
            // 
            AcceptButton = _okButton;
            AutoScaleDimensions = new SizeF(168F, 168F);
            AutoScaleMode = AutoScaleMode.Dpi;
            CancelButton = _cancelButton;
            ClientSize = new Size(648, 830);
            Controls.Add(_cancelButton);
            Controls.Add(_okButton);
            Controls.Add(_infoLabel);
            Controls.Add(_languageComboBox);
            Controls.Add(_languageLabel);
            Controls.Add(_autoStartOnAddCheckBox);
            Controls.Add(_copyTorrentFileCheckBox);
            Controls.Add(_autoStartCheckBox);
            Controls.Add(_minimizeToTrayCheckBox);
            Controls.Add(_enableLoggingCheckBox);
            Controls.Add(_globalUploadLimitComboBox);
            Controls.Add(_globalUploadLimitCheckBox);
            Controls.Add(_globalDownloadLimitComboBox);
            Controls.Add(_globalDownloadLimitCheckBox);
            Controls.Add(_maxRequestsNumeric);
            Controls.Add(_maxRequestsLabel);
            Controls.Add(_maxPiecesNumeric);
            Controls.Add(_maxPiecesLabel);
            Controls.Add(_maxHalfOpenNumeric);
            Controls.Add(_maxHalfOpenLabel);
            Controls.Add(_maxConnectionsNumeric);
            Controls.Add(_maxConnectionsLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "GlobalSettingsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = " Глобальные настройки";
            ((System.ComponentModel.ISupportInitialize)_maxConnectionsNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)_maxHalfOpenNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)_maxPiecesNumeric).EndInit();
            ((System.ComponentModel.ISupportInitialize)_maxRequestsNumeric).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label _maxConnectionsLabel;
        private System.Windows.Forms.NumericUpDown _maxConnectionsNumeric;
        private System.Windows.Forms.Label _maxHalfOpenLabel;
        private System.Windows.Forms.NumericUpDown _maxHalfOpenNumeric;
        private System.Windows.Forms.Label _maxPiecesLabel;
        private System.Windows.Forms.NumericUpDown _maxPiecesNumeric;
        private System.Windows.Forms.Label _maxRequestsLabel;
        private System.Windows.Forms.NumericUpDown _maxRequestsNumeric;
        private System.Windows.Forms.CheckBox _globalDownloadLimitCheckBox;
        private System.Windows.Forms.ComboBox _globalDownloadLimitComboBox;
        private System.Windows.Forms.CheckBox _globalUploadLimitCheckBox;
        private System.Windows.Forms.ComboBox _globalUploadLimitComboBox;
        private System.Windows.Forms.CheckBox _enableLoggingCheckBox;
        private System.Windows.Forms.CheckBox _minimizeToTrayCheckBox;
        private System.Windows.Forms.CheckBox _autoStartCheckBox;
        private System.Windows.Forms.CheckBox _copyTorrentFileCheckBox;
        private System.Windows.Forms.CheckBox _autoStartOnAddCheckBox;
        private System.Windows.Forms.Label _languageLabel;
        private System.Windows.Forms.ComboBox _languageComboBox;
        private System.Windows.Forms.Label _infoLabel;
        private System.Windows.Forms.Button _okButton;
        private System.Windows.Forms.Button _cancelButton;
    }
}
