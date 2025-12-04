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
            this._maxConnectionsLabel = new System.Windows.Forms.Label();
            this._maxConnectionsNumeric = new System.Windows.Forms.NumericUpDown();
            this._maxHalfOpenLabel = new System.Windows.Forms.Label();
            this._maxHalfOpenNumeric = new System.Windows.Forms.NumericUpDown();
            this._maxPiecesLabel = new System.Windows.Forms.Label();
            this._maxPiecesNumeric = new System.Windows.Forms.NumericUpDown();
            this._maxRequestsLabel = new System.Windows.Forms.Label();
            this._maxRequestsNumeric = new System.Windows.Forms.NumericUpDown();
            this._globalDownloadLimitCheckBox = new System.Windows.Forms.CheckBox();
            this._globalDownloadLimitComboBox = new System.Windows.Forms.ComboBox();
            this._globalUploadLimitCheckBox = new System.Windows.Forms.CheckBox();
            this._globalUploadLimitComboBox = new System.Windows.Forms.ComboBox();
            this._enableLoggingCheckBox = new System.Windows.Forms.CheckBox();
            this._minimizeToTrayCheckBox = new System.Windows.Forms.CheckBox();
            this._autoStartCheckBox = new System.Windows.Forms.CheckBox();
            this._copyTorrentFileCheckBox = new System.Windows.Forms.CheckBox();
            this._autoStartOnAddCheckBox = new System.Windows.Forms.CheckBox();
            this._okButton = new System.Windows.Forms.Button();
            this._cancelButton = new System.Windows.Forms.Button();
            this._infoLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this._maxConnectionsNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._maxHalfOpenNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._maxPiecesNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._maxRequestsNumeric)).BeginInit();
            this.SuspendLayout();
            // 
            // _maxConnectionsLabel
            // 
            this._maxConnectionsLabel.AutoSize = true;
            this._maxConnectionsLabel.Location = new System.Drawing.Point(20, 20);
            this._maxConnectionsLabel.Name = "_maxConnectionsLabel";
            this._maxConnectionsLabel.Size = new System.Drawing.Size(200, 15);
            this._maxConnectionsLabel.TabIndex = 0;
            this._maxConnectionsLabel.Text = "Макс. соединений с пирами:";
            // 
            // _maxConnectionsNumeric
            // 
            this._maxConnectionsNumeric.Location = new System.Drawing.Point(270, 18);
            this._maxConnectionsNumeric.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
            this._maxConnectionsNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._maxConnectionsNumeric.Name = "_maxConnectionsNumeric";
            this._maxConnectionsNumeric.Size = new System.Drawing.Size(80, 23);
            this._maxConnectionsNumeric.TabIndex = 1;
            this._maxConnectionsNumeric.Value = new decimal(new int[] { 200, 0, 0, 0 });
            // 
            // _maxHalfOpenLabel
            // 
            this._maxHalfOpenLabel.AutoSize = true;
            this._maxHalfOpenLabel.Location = new System.Drawing.Point(20, 55);
            this._maxHalfOpenLabel.Name = "_maxHalfOpenLabel";
            this._maxHalfOpenLabel.Size = new System.Drawing.Size(200, 15);
            this._maxHalfOpenLabel.TabIndex = 2;
            this._maxHalfOpenLabel.Text = "Макс. полуоткрытых соединений:";
            // 
            // _maxHalfOpenNumeric
            // 
            this._maxHalfOpenNumeric.Location = new System.Drawing.Point(270, 53);
            this._maxHalfOpenNumeric.Maximum = new decimal(new int[] { 2000, 0, 0, 0 });
            this._maxHalfOpenNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._maxHalfOpenNumeric.Name = "_maxHalfOpenNumeric";
            this._maxHalfOpenNumeric.Size = new System.Drawing.Size(80, 23);
            this._maxHalfOpenNumeric.TabIndex = 3;
            this._maxHalfOpenNumeric.Value = new decimal(new int[] { 50, 0, 0, 0 });
            // 
            // _maxPiecesLabel
            // 
            this._maxPiecesLabel.AutoSize = true;
            this._maxPiecesLabel.Location = new System.Drawing.Point(20, 90);
            this._maxPiecesLabel.Name = "_maxPiecesLabel";
            this._maxPiecesLabel.Size = new System.Drawing.Size(200, 15);
            this._maxPiecesLabel.TabIndex = 4;
            this._maxPiecesLabel.Text = "Кусков одновременно:";
            // 
            // _maxPiecesNumeric
            // 
            this._maxPiecesNumeric.Location = new System.Drawing.Point(270, 88);
            this._maxPiecesNumeric.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            this._maxPiecesNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._maxPiecesNumeric.Name = "_maxPiecesNumeric";
            this._maxPiecesNumeric.Size = new System.Drawing.Size(80, 23);
            this._maxPiecesNumeric.TabIndex = 5;
            this._maxPiecesNumeric.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // _maxRequestsLabel
            // 
            this._maxRequestsLabel.AutoSize = true;
            this._maxRequestsLabel.Location = new System.Drawing.Point(20, 125);
            this._maxRequestsLabel.Name = "_maxRequestsLabel";
            this._maxRequestsLabel.Size = new System.Drawing.Size(200, 15);
            this._maxRequestsLabel.TabIndex = 6;
            this._maxRequestsLabel.Text = "Запросов на соединение:";
            // 
            // _maxRequestsNumeric
            // 
            this._maxRequestsNumeric.Location = new System.Drawing.Point(270, 123);
            this._maxRequestsNumeric.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
            this._maxRequestsNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._maxRequestsNumeric.Name = "_maxRequestsNumeric";
            this._maxRequestsNumeric.Size = new System.Drawing.Size(80, 23);
            this._maxRequestsNumeric.TabIndex = 7;
            this._maxRequestsNumeric.Value = new decimal(new int[] { 16, 0, 0, 0 });
            // 
            // _globalDownloadLimitCheckBox
            // 
            this._globalDownloadLimitCheckBox.AutoSize = true;
            this._globalDownloadLimitCheckBox.Location = new System.Drawing.Point(20, 160);
            this._globalDownloadLimitCheckBox.Name = "_globalDownloadLimitCheckBox";
            this._globalDownloadLimitCheckBox.Size = new System.Drawing.Size(200, 19);
            this._globalDownloadLimitCheckBox.TabIndex = 8;
            this._globalDownloadLimitCheckBox.Text = "Лимит загрузки (Mbps):";
            this._globalDownloadLimitCheckBox.UseVisualStyleBackColor = true;
            // 
            // _globalDownloadLimitComboBox
            // 
            this._globalDownloadLimitComboBox.FormattingEnabled = true;
            this._globalDownloadLimitComboBox.Items.AddRange(new object[] {
            "1",
            "2",
            "5",
            "10",
            "20",
            "50",
            "100",
            "200",
            "500",
            "1000"});
            this._globalDownloadLimitComboBox.Location = new System.Drawing.Point(270, 158);
            this._globalDownloadLimitComboBox.Name = "_globalDownloadLimitComboBox";
            this._globalDownloadLimitComboBox.Size = new System.Drawing.Size(80, 23);
            this._globalDownloadLimitComboBox.TabIndex = 9;
            // 
            // _globalUploadLimitCheckBox
            // 
            this._globalUploadLimitCheckBox.AutoSize = true;
            this._globalUploadLimitCheckBox.Location = new System.Drawing.Point(20, 190);
            this._globalUploadLimitCheckBox.Name = "_globalUploadLimitCheckBox";
            this._globalUploadLimitCheckBox.Size = new System.Drawing.Size(200, 19);
            this._globalUploadLimitCheckBox.TabIndex = 10;
            this._globalUploadLimitCheckBox.Text = "Лимит отдачи (Mbps):";
            this._globalUploadLimitCheckBox.UseVisualStyleBackColor = true;
            // 
            // _globalUploadLimitComboBox
            // 
            this._globalUploadLimitComboBox.FormattingEnabled = true;
            this._globalUploadLimitComboBox.Items.AddRange(new object[] {
            "1",
            "2",
            "5",
            "10",
            "20",
            "50",
            "100",
            "200",
            "500",
            "1000"});
            this._globalUploadLimitComboBox.Location = new System.Drawing.Point(270, 188);
            this._globalUploadLimitComboBox.Name = "_globalUploadLimitComboBox";
            this._globalUploadLimitComboBox.Size = new System.Drawing.Size(80, 23);
            this._globalUploadLimitComboBox.TabIndex = 11;
            // 
            // _enableLoggingCheckBox
            // 
            this._enableLoggingCheckBox.AutoSize = true;
            this._enableLoggingCheckBox.Location = new System.Drawing.Point(20, 220);
            this._enableLoggingCheckBox.Name = "_enableLoggingCheckBox";
            this._enableLoggingCheckBox.Size = new System.Drawing.Size(200, 19);
            this._enableLoggingCheckBox.TabIndex = 12;
            this._enableLoggingCheckBox.Text = "Включить логирование";
            this._enableLoggingCheckBox.UseVisualStyleBackColor = true;
            // 
            // _minimizeToTrayCheckBox
            // 
            this._minimizeToTrayCheckBox.AutoSize = true;
            this._minimizeToTrayCheckBox.Location = new System.Drawing.Point(20, 245);
            this._minimizeToTrayCheckBox.Name = "_minimizeToTrayCheckBox";
            this._minimizeToTrayCheckBox.Size = new System.Drawing.Size(280, 19);
            this._minimizeToTrayCheckBox.TabIndex = 13;
            this._minimizeToTrayCheckBox.Text = "Сворачивать в трей при закрытии окна";
            this._minimizeToTrayCheckBox.UseVisualStyleBackColor = true;
            // 
            // _autoStartCheckBox
            // 
            this._autoStartCheckBox.AutoSize = true;
            this._autoStartCheckBox.Location = new System.Drawing.Point(20, 270);
            this._autoStartCheckBox.Name = "_autoStartCheckBox";
            this._autoStartCheckBox.Size = new System.Drawing.Size(310, 19);
            this._autoStartCheckBox.TabIndex = 14;
            this._autoStartCheckBox.Text = "Автозапуск торрентов при открытии приложения";
            this._autoStartCheckBox.UseVisualStyleBackColor = true;
            // 
            // _copyTorrentFileCheckBox
            // 
            this._copyTorrentFileCheckBox.AutoSize = true;
            this._copyTorrentFileCheckBox.Location = new System.Drawing.Point(20, 295);
            this._copyTorrentFileCheckBox.Name = "_copyTorrentFileCheckBox";
            this._copyTorrentFileCheckBox.Size = new System.Drawing.Size(320, 19);
            this._copyTorrentFileCheckBox.TabIndex = 15;
            this._copyTorrentFileCheckBox.Text = "Копировать .torrent файл в папку загрузки";
            this._copyTorrentFileCheckBox.UseVisualStyleBackColor = true;
            // 
            // _autoStartOnAddCheckBox
            // 
            this._autoStartOnAddCheckBox.AutoSize = true;
            this._autoStartOnAddCheckBox.Location = new System.Drawing.Point(20, 320);
            this._autoStartOnAddCheckBox.Name = "_autoStartOnAddCheckBox";
            this._autoStartOnAddCheckBox.Size = new System.Drawing.Size(320, 19);
            this._autoStartOnAddCheckBox.TabIndex = 16;
            this._autoStartOnAddCheckBox.Text = "Автоматически запускать торрент при добавлении";
            this._autoStartOnAddCheckBox.UseVisualStyleBackColor = true;
            // 
            // _infoLabel
            // 
            this._infoLabel.ForeColor = System.Drawing.Color.Gray;
            this._infoLabel.Location = new System.Drawing.Point(20, 350);
            this._infoLabel.Name = "_infoLabel";
            this._infoLabel.Size = new System.Drawing.Size(330, 35);
            this._infoLabel.TabIndex = 17;
            this._infoLabel.Text = "Увеличение параллелизма может повысить скорость, но создаёт больше нагрузки на систему.";
            // 
            // _okButton
            // 
            this._okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._okButton.Location = new System.Drawing.Point(155, 395);
            this._okButton.Name = "_okButton";
            this._okButton.Size = new System.Drawing.Size(90, 30);
            this._okButton.TabIndex = 18;
            this._okButton.Text = "Сохранить";
            this._okButton.UseVisualStyleBackColor = true;
            // 
            // _cancelButton
            // 
            this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._cancelButton.Location = new System.Drawing.Point(255, 395);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Size = new System.Drawing.Size(90, 30);
            this._cancelButton.TabIndex = 19;
            this._cancelButton.Text = "Отмена";
            this._cancelButton.UseVisualStyleBackColor = true;
            // 
            // GlobalSettingsForm
            // 
            this.AcceptButton = this._okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this._cancelButton;
            this.ClientSize = new System.Drawing.Size(370, 440);
            this.Controls.Add(this._cancelButton);
            this.Controls.Add(this._okButton);
            this.Controls.Add(this._infoLabel);
            this.Controls.Add(this._autoStartOnAddCheckBox);
            this.Controls.Add(this._copyTorrentFileCheckBox);
            this.Controls.Add(this._autoStartCheckBox);
            this.Controls.Add(this._minimizeToTrayCheckBox);
            this.Controls.Add(this._enableLoggingCheckBox);
            this.Controls.Add(this._globalUploadLimitComboBox);
            this.Controls.Add(this._globalUploadLimitCheckBox);
            this.Controls.Add(this._globalDownloadLimitComboBox);
            this.Controls.Add(this._globalDownloadLimitCheckBox);
            this.Controls.Add(this._maxRequestsNumeric);
            this.Controls.Add(this._maxRequestsLabel);
            this.Controls.Add(this._maxPiecesNumeric);
            this.Controls.Add(this._maxPiecesLabel);
            this.Controls.Add(this._maxHalfOpenNumeric);
            this.Controls.Add(this._maxHalfOpenLabel);
            this.Controls.Add(this._maxConnectionsNumeric);
            this.Controls.Add(this._maxConnectionsLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GlobalSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Глобальные настройки";
            ((System.ComponentModel.ISupportInitialize)(this._maxConnectionsNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._maxHalfOpenNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._maxPiecesNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._maxRequestsNumeric)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
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
        private System.Windows.Forms.Label _infoLabel;
        private System.Windows.Forms.Button _okButton;
        private System.Windows.Forms.Button _cancelButton;
    }
}
