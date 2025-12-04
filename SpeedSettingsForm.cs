using System;
using System.Windows.Forms;
using TorrentClient.Models;

namespace TorrentClient
{
    public partial class SpeedSettingsForm : Form
    {
        private readonly Torrent _torrent;
        
        // Предустановленные значения скорости (в Mbps)
        private static readonly string[] SpeedPresets = new[]
        {
            "Без ограничений",
            "1",      // 1 Mbps
            "2",      // 2 Mbps
            "5",      // 5 Mbps
            "10",     // 10 Mbps
            "20",     // 20 Mbps
            "50",     // 50 Mbps
            "100",    // 100 Mbps
            "200",    // 200 Mbps
            "500",    // 500 Mbps
            "1000",   // 1000 Mbps
            "Другое..."
        };

        public long? MaxDownloadSpeed { get; private set; }
        public long? MaxUploadSpeed { get; private set; }

        public SpeedSettingsForm(Torrent torrent)
        {
            _torrent = torrent;
            InitializeComponent();
            SetupComboBoxes();
            LoadSettings();
        }
        
        private void SetupComboBoxes()
        {
            // Настройка ComboBox для загрузки (только Mbps)
            _downloadSpeedComboBox.Items.AddRange(SpeedPresets);
            _downloadUnitComboBox.Items.Clear();
            _downloadUnitComboBox.Items.Add("Mbps");
            _downloadUnitComboBox.SelectedIndex = 0;
            _downloadUnitComboBox.Enabled = false; // Только Mbps, не редактируется
            
            // Настройка ComboBox для отдачи (только Mbps)
            _uploadSpeedComboBox.Items.AddRange(SpeedPresets);
            _uploadUnitComboBox.Items.Clear();
            _uploadUnitComboBox.Items.Add("Mbps");
            _uploadUnitComboBox.SelectedIndex = 0;
            _uploadUnitComboBox.Enabled = false; // Только Mbps, не редактируется
        }

        private void LoadSettings()
        {
            if (_torrent.MaxDownloadSpeed.HasValue)
            {
                LoadSpeedValue(_torrent.MaxDownloadSpeed.Value, 
                    _downloadSpeedComboBox, _downloadUnitComboBox, _downloadCustomTextBox);
            }
            else
            {
                _downloadSpeedComboBox.SelectedIndex = 0; // Без ограничений
                _downloadCustomTextBox.Enabled = false;
            }

            if (_torrent.MaxUploadSpeed.HasValue)
            {
                LoadSpeedValue(_torrent.MaxUploadSpeed.Value, 
                    _uploadSpeedComboBox, _uploadUnitComboBox, _uploadCustomTextBox);
            }
            else
            {
                _uploadSpeedComboBox.SelectedIndex = 0; // Без ограничений
                _uploadCustomTextBox.Enabled = false;
            }
        }
        
        private void LoadSpeedValue(long bytesPerSecond, ComboBox speedCombo, ComboBox unitCombo, TextBox customTextBox)
        {
            // Конвертируем байты/сек в Mbps (обратная конвертация)
            // 1 байт/сек = 8 бит/сек
            // 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
            // Формула: Mbps = (bytesPerSecond * 8) / 1,000,000
            double bitsPerSecond = bytesPerSecond * 8.0;
            double mbps = bitsPerSecond / 1_000_000.0;
            
            // Округляем до ближайшего целого для отображения
            int mbpsInt = (int)Math.Round(mbps);
            
            var mbpsStr = mbpsInt.ToString();
            
            int presetIndex = Array.FindIndex(SpeedPresets, s => s == mbpsStr);
            if (presetIndex >= 0)
            {
                speedCombo.SelectedIndex = presetIndex;
                customTextBox.Enabled = false;
                customTextBox.Text = string.Empty;
            }
            else
            {
                speedCombo.SelectedIndex = SpeedPresets.Length - 1; // Другое...
                customTextBox.Text = mbpsInt.ToString();
                customTextBox.Enabled = true;
            }
        }

        private void DownloadSpeedComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var isUnlimited = _downloadSpeedComboBox.SelectedIndex == 0;
            var isCustom = _downloadSpeedComboBox.SelectedIndex == SpeedPresets.Length - 1;
            
            _downloadCustomTextBox.Enabled = isCustom;
            
            if (!isCustom)
            {
                _downloadCustomTextBox.Text = string.Empty;
            }
        }

        private void UploadSpeedComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var isUnlimited = _uploadSpeedComboBox.SelectedIndex == 0;
            var isCustom = _uploadSpeedComboBox.SelectedIndex == SpeedPresets.Length - 1;
            
            _uploadCustomTextBox.Enabled = isCustom;
            
            if (!isCustom)
            {
                _uploadCustomTextBox.Text = string.Empty;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                MaxDownloadSpeed = GetSpeedValue(_downloadSpeedComboBox, _downloadUnitComboBox, _downloadCustomTextBox);
                MaxUploadSpeed = GetSpeedValue(_uploadSpeedComboBox, _uploadUnitComboBox, _uploadCustomTextBox);
            }

            base.OnFormClosing(e);
        }
        
        private long? GetSpeedValue(ComboBox speedCombo, ComboBox unitCombo, TextBox customTextBox)
        {
            // Без ограничений
            if (speedCombo.SelectedIndex == 0)
            {
                return null;
            }
            
            string valueStr;
            
            // Другое...
            if (speedCombo.SelectedIndex == SpeedPresets.Length - 1)
            {
                valueStr = customTextBox.Text.Trim();
            }
            else
            {
                valueStr = SpeedPresets[speedCombo.SelectedIndex];
            }
            
            if (int.TryParse(valueStr, out int mbpsValue) && mbpsValue > 0)
            {
                // Конвертируем Mbps в байты/сек (точная конвертация)
                // 1 Mbps = 1,000,000 бит/сек = 1,000,000 / 8 = 125,000 байт/сек
                // Примеры:
                //   1 Mbps = 125,000 байт/сек
                //   10 Mbps = 1,250,000 байт/сек
                //   100 Mbps = 12,500,000 байт/сек
                return (long)(mbpsValue * 1_000_000.0 / 8.0);
            }
            
            return null;
        }
    }
}
