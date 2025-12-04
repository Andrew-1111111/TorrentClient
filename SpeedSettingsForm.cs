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
            "Другое..."
        };
        
        // Единицы измерения: мегабиты и килобиты в секунду
        private static readonly string[] UnitOptions = new[] { "Kbps", "Mbps" };

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
            // Настройка ComboBox для загрузки
            _downloadSpeedComboBox.Items.AddRange(SpeedPresets);
            _downloadUnitComboBox.Items.AddRange(UnitOptions);
            _downloadUnitComboBox.SelectedIndex = 1; // Mbps по умолчанию
            
            // Настройка ComboBox для отдачи
            _uploadSpeedComboBox.Items.AddRange(SpeedPresets);
            _uploadUnitComboBox.Items.AddRange(UnitOptions);
            _uploadUnitComboBox.SelectedIndex = 1; // Mbps по умолчанию
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
                _downloadUnitComboBox.Enabled = false;
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
                _uploadUnitComboBox.Enabled = false;
            }
        }
        
        private void LoadSpeedValue(long bytesPerSecond, ComboBox speedCombo, ComboBox unitCombo, TextBox customTextBox)
        {
            // Конвертируем байты/сек в биты/сек, затем в Kbps и Mbps
            long bitsPerSecond = bytesPerSecond * 8;
            long kbps = bitsPerSecond / 1000;
            long mbps = bitsPerSecond / 1_000_000;
            
            if (mbps >= 1)
            {
                // Используем Mbps
                unitCombo.SelectedIndex = 1;
                var mbpsStr = mbps.ToString();
                
                int presetIndex = Array.FindIndex(SpeedPresets, s => s == mbpsStr);
                if (presetIndex >= 0)
                {
                    speedCombo.SelectedIndex = presetIndex;
                    customTextBox.Enabled = false;
                }
                else
                {
                    speedCombo.SelectedIndex = SpeedPresets.Length - 1; // Другое...
                    customTextBox.Text = mbpsStr;
                    customTextBox.Enabled = true;
                }
            }
            else
            {
                // Используем Kbps
                unitCombo.SelectedIndex = 0;
                var kbpsStr = kbps.ToString();
                
                int presetIndex = Array.FindIndex(SpeedPresets, s => s == kbpsStr);
                if (presetIndex >= 0)
                {
                    speedCombo.SelectedIndex = presetIndex;
                    customTextBox.Enabled = false;
                }
                else
                {
                    speedCombo.SelectedIndex = SpeedPresets.Length - 1; // Другое...
                    customTextBox.Text = kbpsStr;
                    customTextBox.Enabled = true;
                }
            }
        }

        private void DownloadSpeedComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var isUnlimited = _downloadSpeedComboBox.SelectedIndex == 0;
            var isCustom = _downloadSpeedComboBox.SelectedIndex == SpeedPresets.Length - 1;
            
            _downloadCustomTextBox.Enabled = isCustom;
            _downloadUnitComboBox.Enabled = !isUnlimited;
            
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
            _uploadUnitComboBox.Enabled = !isUnlimited;
            
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
                valueStr = customTextBox.Text;
            }
            else
            {
                valueStr = SpeedPresets[speedCombo.SelectedIndex];
            }
            
            if (long.TryParse(valueStr, out long value) && value > 0)
            {
                // Конвертируем биты/сек в байты/сек
                // 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
                // 1 Kbps = 1,000 бит/сек = 125 байт/сек
                if (unitCombo.SelectedIndex == 1) // Mbps
                {
                    return value * 125_000; // Mbps -> bytes/sec
                }
                else // Kbps
                {
                    return value * 125; // Kbps -> bytes/sec
                }
            }
            
            return null;
        }
    }
}
