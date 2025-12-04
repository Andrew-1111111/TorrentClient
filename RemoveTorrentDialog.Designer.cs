namespace TorrentClient
{
    partial class RemoveTorrentDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RemoveTorrentDialog));
            _iconBox = new PictureBox();
            _messageLabel = new Label();
            _deleteFilesCheckBox = new CheckBox();
            _yesButton = new Button();
            _noButton = new Button();
            ((System.ComponentModel.ISupportInitialize)_iconBox).BeginInit();
            SuspendLayout();
            // 
            // _iconBox
            // 
            _iconBox.Image = (Image)resources.GetObject("_iconBox.Image");
            _iconBox.Location = new Point(34, 40);
            _iconBox.Margin = new Padding(5, 6, 5, 6);
            _iconBox.Name = "_iconBox";
            _iconBox.Size = new Size(82, 96);
            _iconBox.SizeMode = PictureBoxSizeMode.Zoom;
            _iconBox.TabIndex = 0;
            _iconBox.TabStop = false;
            // 
            // _messageLabel
            // 
            _messageLabel.Font = new Font("Segoe UI", 10F);
            _messageLabel.Location = new Point(137, 40);
            _messageLabel.Margin = new Padding(5, 0, 5, 0);
            _messageLabel.Name = "_messageLabel";
            _messageLabel.Size = new Size(552, 50);
            _messageLabel.TabIndex = 1;
            _messageLabel.Text = "Вы уверены, что хотите удалить этот торрент?";
            // 
            // _deleteFilesCheckBox
            // 
            _deleteFilesCheckBox.Location = new Point(137, 86);
            _deleteFilesCheckBox.Margin = new Padding(5, 6, 5, 6);
            _deleteFilesCheckBox.Name = "_deleteFilesCheckBox";
            _deleteFilesCheckBox.Size = new Size(518, 50);
            _deleteFilesCheckBox.TabIndex = 2;
            _deleteFilesCheckBox.Text = "Также удалить загруженные файлы";
            _deleteFilesCheckBox.UseVisualStyleBackColor = true;
            // 
            // _yesButton
            // 
            _yesButton.DialogResult = DialogResult.Yes;
            _yesButton.Location = new Point(166, 148);
            _yesButton.Margin = new Padding(5, 6, 5, 6);
            _yesButton.Name = "_yesButton";
            _yesButton.Size = new Size(171, 51);
            _yesButton.TabIndex = 3;
            _yesButton.Text = "Да";
            _yesButton.UseVisualStyleBackColor = true;
            // 
            // _noButton
            // 
            _noButton.DialogResult = DialogResult.No;
            _noButton.Location = new Point(347, 148);
            _noButton.Margin = new Padding(5, 6, 5, 6);
            _noButton.Name = "_noButton";
            _noButton.Size = new Size(171, 51);
            _noButton.TabIndex = 4;
            _noButton.Text = "Нет";
            _noButton.UseVisualStyleBackColor = true;
            // 
            // RemoveTorrentDialog
            // 
            AcceptButton = _yesButton;
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = _noButton;
            ClientSize = new Size(691, 232);
            Controls.Add(_noButton);
            Controls.Add(_yesButton);
            Controls.Add(_deleteFilesCheckBox);
            Controls.Add(_messageLabel);
            Controls.Add(_iconBox);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(5, 6, 5, 6);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "RemoveTorrentDialog";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = " Удаление торрента";
            ((System.ComponentModel.ISupportInitialize)_iconBox).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.PictureBox _iconBox;
        private System.Windows.Forms.Label _messageLabel;
        private System.Windows.Forms.CheckBox _deleteFilesCheckBox;
        private System.Windows.Forms.Button _yesButton;
        private System.Windows.Forms.Button _noButton;
    }
}

