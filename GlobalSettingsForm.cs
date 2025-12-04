namespace TorrentClient
{
    /// <summary>
    /// Форма глобальных настроек приложения
    /// </summary>
    public partial class GlobalSettingsForm : Form
    {
        private readonly AppSettings _settings;

        public int MaxConnections => (int)_maxConnectionsNumeric.Value;
        public int MaxHalfOpenConnections => (int)_maxHalfOpenNumeric.Value;
        public int MaxPiecesToRequest => (int)_maxPiecesNumeric.Value;
        public int MaxRequestsPerPeer => (int)_maxRequestsNumeric.Value;
        public bool EnableLogging => _enableLoggingCheckBox.Checked;
        public bool MinimizeToTrayOnClose => _minimizeToTrayCheckBox.Checked;
        public bool AutoStartOnLaunch => _autoStartCheckBox.Checked;
        public bool AutoStartOnAdd => _autoStartOnAddCheckBox.Checked;
        public bool CopyTorrentFileToDownloadFolder => _copyTorrentFileCheckBox.Checked;
        
        /// <summary>Глобальный лимит скорости загрузки (байт/сек, null = без ограничений)</summary>
        public long? GlobalMaxDownloadSpeed => _globalDownloadLimitCheckBox.Checked 
            ? (long)(GetMbpsFromComboBox(_globalDownloadLimitComboBox) * 1024 * 1024 / 8) // Mbps -> bytes/sec
            : null;
        
        /// <summary>Глобальный лимит скорости отдачи (байт/сек, null = без ограничений)</summary>
        public long? GlobalMaxUploadSpeed => _globalUploadLimitCheckBox.Checked 
            ? (long)(GetMbpsFromComboBox(_globalUploadLimitComboBox) * 1024 * 1024 / 8) // Mbps -> bytes/sec
            : null;
        
        private static int GetMbpsFromComboBox(ComboBox comboBox)
        {
            // Пробуем распарсить введённое значение
            var text = comboBox.Text.Trim();
            if (int.TryParse(text, out int value) && value > 0)
                return Math.Min(value, 10000); // Ограничиваем максимум 10000 Mbps
            return 100; // default
        }
        
        private static void SetComboBoxFromMbps(ComboBox comboBox, int mbps)
        {
            // Устанавливаем текст напрямую - если значение есть в списке, оно будет выбрано
            comboBox.Text = mbps.ToString();
        }

        public GlobalSettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _maxConnectionsNumeric.Value = Math.Clamp(_settings.MaxConnections, 1, 5000);
            _maxHalfOpenNumeric.Value = Math.Clamp(_settings.MaxHalfOpenConnections, 1, 2000);
            _maxPiecesNumeric.Value = Math.Clamp(_settings.MaxPiecesToRequest, 1, 500);
            _maxRequestsNumeric.Value = Math.Clamp(_settings.MaxRequestsPerPeer, 1, 500);
            _enableLoggingCheckBox.Checked = _settings.EnableLogging;
            _minimizeToTrayCheckBox.Checked = _settings.MinimizeToTrayOnClose;
            _autoStartCheckBox.Checked = _settings.AutoStartOnLaunch;
            _autoStartOnAddCheckBox.Checked = _settings.AutoStartOnAdd;
            _copyTorrentFileCheckBox.Checked = _settings.CopyTorrentFileToDownloadFolder;
            
            // Глобальные лимиты скорости
            _globalDownloadLimitCheckBox.Checked = _settings.GlobalMaxDownloadSpeed.HasValue;
            _globalUploadLimitCheckBox.Checked = _settings.GlobalMaxUploadSpeed.HasValue;
            
            // Установить значения для ComboBox
            if (_settings.GlobalMaxDownloadSpeed.HasValue)
            {
                int mbps = (int)(_settings.GlobalMaxDownloadSpeed.Value * 8 / 1024 / 1024);
                SetComboBoxFromMbps(_globalDownloadLimitComboBox, mbps);
            }
            else
            {
                _globalDownloadLimitComboBox.Text = "100"; // default
            }
            
            if (_settings.GlobalMaxUploadSpeed.HasValue)
            {
                int mbps = (int)(_settings.GlobalMaxUploadSpeed.Value * 8 / 1024 / 1024);
                SetComboBoxFromMbps(_globalUploadLimitComboBox, mbps);
            }
            else
            {
                _globalUploadLimitComboBox.Text = "100"; // default
            }
        }
    }
}
