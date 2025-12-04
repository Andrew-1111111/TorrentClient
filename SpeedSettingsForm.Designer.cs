namespace TorrentClient
{
    partial class SpeedSettingsForm
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
            if (disposing && (components != null))
            {
                components.Dispose();
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
            _downloadLabel = new Label();
            _downloadSpeedComboBox = new ComboBox();
            _downloadUnitComboBox = new ComboBox();
            _downloadCustomTextBox = new TextBox();
            _downloadCustomLabel = new Label();
            _uploadLabel = new Label();
            _uploadSpeedComboBox = new ComboBox();
            _uploadUnitComboBox = new ComboBox();
            _uploadCustomTextBox = new TextBox();
            _uploadCustomLabel = new Label();
            _okButton = new Button();
            _cancelButton = new Button();
            SuspendLayout();
            // 
            // _downloadLabel
            // 
            _downloadLabel.AutoSize = true;
            _downloadLabel.Font = new Font("Segoe UI", 9.5F);
            _downloadLabel.Location = new Point(34, 40);
            _downloadLabel.Margin = new Padding(5, 0, 5, 0);
            _downloadLabel.Name = "_downloadLabel";
            _downloadLabel.Size = new Size(275, 31);
            _downloadLabel.TabIndex = 0;
            _downloadLabel.Text = "Макс. скорость загрузки:";
            // 
            // _downloadSpeedComboBox
            // 
            _downloadSpeedComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _downloadSpeedComboBox.Location = new Point(34, 90);
            _downloadSpeedComboBox.Margin = new Padding(5, 6, 5, 6);
            _downloadSpeedComboBox.Name = "_downloadSpeedComboBox";
            _downloadSpeedComboBox.Size = new Size(306, 38);
            _downloadSpeedComboBox.TabIndex = 1;
            _downloadSpeedComboBox.SelectedIndexChanged += DownloadSpeedComboBox_SelectedIndexChanged;
            // 
            // _downloadUnitComboBox
            // 
            _downloadUnitComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _downloadUnitComboBox.Location = new Point(360, 90);
            _downloadUnitComboBox.Margin = new Padding(5, 6, 5, 6);
            _downloadUnitComboBox.Name = "_downloadUnitComboBox";
            _downloadUnitComboBox.Size = new Size(134, 38);
            _downloadUnitComboBox.TabIndex = 2;
            // 
            // _downloadCustomTextBox
            // 
            _downloadCustomTextBox.Enabled = false;
            _downloadCustomTextBox.Location = new Point(240, 154);
            _downloadCustomTextBox.Margin = new Padding(5, 6, 5, 6);
            _downloadCustomTextBox.Name = "_downloadCustomTextBox";
            _downloadCustomTextBox.Size = new Size(254, 35);
            _downloadCustomTextBox.TabIndex = 4;
            // 
            // _downloadCustomLabel
            // 
            _downloadCustomLabel.AutoSize = true;
            _downloadCustomLabel.Location = new Point(34, 160);
            _downloadCustomLabel.Margin = new Padding(5, 0, 5, 0);
            _downloadCustomLabel.Name = "_downloadCustomLabel";
            _downloadCustomLabel.Size = new Size(161, 30);
            _downloadCustomLabel.TabIndex = 3;
            _downloadCustomLabel.Text = "Своё значение:";
            // 
            // _uploadLabel
            // 
            _uploadLabel.AutoSize = true;
            _uploadLabel.Font = new Font("Segoe UI", 9.5F);
            _uploadLabel.Location = new Point(34, 240);
            _uploadLabel.Margin = new Padding(5, 0, 5, 0);
            _uploadLabel.Name = "_uploadLabel";
            _uploadLabel.Size = new Size(258, 31);
            _uploadLabel.TabIndex = 5;
            _uploadLabel.Text = "Макс. скорость отдачи:";
            // 
            // _uploadSpeedComboBox
            // 
            _uploadSpeedComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _uploadSpeedComboBox.Location = new Point(34, 290);
            _uploadSpeedComboBox.Margin = new Padding(5, 6, 5, 6);
            _uploadSpeedComboBox.Name = "_uploadSpeedComboBox";
            _uploadSpeedComboBox.Size = new Size(306, 38);
            _uploadSpeedComboBox.TabIndex = 6;
            _uploadSpeedComboBox.SelectedIndexChanged += UploadSpeedComboBox_SelectedIndexChanged;
            // 
            // _uploadUnitComboBox
            // 
            _uploadUnitComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _uploadUnitComboBox.Location = new Point(360, 290);
            _uploadUnitComboBox.Margin = new Padding(5, 6, 5, 6);
            _uploadUnitComboBox.Name = "_uploadUnitComboBox";
            _uploadUnitComboBox.Size = new Size(134, 38);
            _uploadUnitComboBox.TabIndex = 7;
            // 
            // _uploadCustomTextBox
            // 
            _uploadCustomTextBox.Enabled = false;
            _uploadCustomTextBox.Location = new Point(240, 354);
            _uploadCustomTextBox.Margin = new Padding(5, 6, 5, 6);
            _uploadCustomTextBox.Name = "_uploadCustomTextBox";
            _uploadCustomTextBox.Size = new Size(254, 35);
            _uploadCustomTextBox.TabIndex = 9;
            // 
            // _uploadCustomLabel
            // 
            _uploadCustomLabel.AutoSize = true;
            _uploadCustomLabel.Location = new Point(34, 360);
            _uploadCustomLabel.Margin = new Padding(5, 0, 5, 0);
            _uploadCustomLabel.Name = "_uploadCustomLabel";
            _uploadCustomLabel.Size = new Size(161, 30);
            _uploadCustomLabel.TabIndex = 8;
            _uploadCustomLabel.Text = "Своё значение:";
            // 
            // _okButton
            // 
            _okButton.DialogResult = DialogResult.OK;
            _okButton.Location = new Point(138, 440);
            _okButton.Margin = new Padding(5, 6, 5, 6);
            _okButton.Name = "_okButton";
            _okButton.Size = new Size(171, 64);
            _okButton.TabIndex = 10;
            _okButton.Text = "OK";
            _okButton.UseVisualStyleBackColor = true;
            // 
            // _cancelButton
            // 
            _cancelButton.DialogResult = DialogResult.Cancel;
            _cancelButton.Location = new Point(323, 440);
            _cancelButton.Margin = new Padding(5, 6, 5, 6);
            _cancelButton.Name = "_cancelButton";
            _cancelButton.Size = new Size(171, 64);
            _cancelButton.TabIndex = 11;
            _cancelButton.Text = "Отмена";
            _cancelButton.UseVisualStyleBackColor = true;
            // 
            // SpeedSettingsForm
            // 
            AcceptButton = _okButton;
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = _cancelButton;
            ClientSize = new Size(531, 540);
            Controls.Add(_cancelButton);
            Controls.Add(_okButton);
            Controls.Add(_uploadCustomTextBox);
            Controls.Add(_uploadCustomLabel);
            Controls.Add(_uploadUnitComboBox);
            Controls.Add(_uploadSpeedComboBox);
            Controls.Add(_uploadLabel);
            Controls.Add(_downloadCustomTextBox);
            Controls.Add(_downloadCustomLabel);
            Controls.Add(_downloadUnitComboBox);
            Controls.Add(_downloadSpeedComboBox);
            Controls.Add(_downloadLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Margin = new Padding(5, 6, 5, 6);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SpeedSettingsForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Настройки скорости";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label _downloadLabel;
        private System.Windows.Forms.ComboBox _downloadSpeedComboBox;
        private System.Windows.Forms.ComboBox _downloadUnitComboBox;
        private System.Windows.Forms.Label _downloadCustomLabel;
        private System.Windows.Forms.TextBox _downloadCustomTextBox;
        private System.Windows.Forms.Label _uploadLabel;
        private System.Windows.Forms.ComboBox _uploadSpeedComboBox;
        private System.Windows.Forms.ComboBox _uploadUnitComboBox;
        private System.Windows.Forms.Label _uploadCustomLabel;
        private System.Windows.Forms.TextBox _uploadCustomTextBox;
        private System.Windows.Forms.Button _okButton;
        private System.Windows.Forms.Button _cancelButton;
    }
}
