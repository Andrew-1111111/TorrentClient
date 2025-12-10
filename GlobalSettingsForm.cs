using TorrentClient.Core;

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
        
        /// <summary>Код языка интерфейса (например, "en", "ru", "es")</summary>
        public string? LanguageCode
        {
            get
            {
                if (_languageComboBox.SelectedItem is LanguageInfo langInfo)
                    return langInfo.Code;
                return null;
            }
        }
        
        /// <summary>Глобальный лимит скорости загрузки (байт/сек, null = без ограничений)</summary>
        public long? GlobalMaxDownloadSpeed
        {
            get
            {
                if (!_globalDownloadLimitCheckBox.Checked)
                    return null;
                
                int mbps = GetMbpsFromComboBox(_globalDownloadLimitComboBox);
                // Конвертируем Mbps в bytes/sec: mbps * 1,000,000 / 8
                // 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
                return (long)(mbps * 1_000_000.0 / 8.0);
            }
        }
        
        /// <summary>Глобальный лимит скорости отдачи (байт/сек, null = без ограничений)</summary>
        public long? GlobalMaxUploadSpeed
        {
            get
            {
                if (!_globalUploadLimitCheckBox.Checked)
                    return null;
                
                int mbps = GetMbpsFromComboBox(_globalUploadLimitComboBox);
                // Конвертируем Mbps в bytes/sec: mbps * 1,000,000 / 8
                // 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
                return (long)(mbps * 1_000_000.0 / 8.0);
            }
        }
        
        private static int GetMbpsFromComboBox(ComboBox comboBox)
        {
            // Сначала проверяем, выбрано ли значение из списка
            if (comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count)
            {
                var selectedItem = comboBox.Items[comboBox.SelectedIndex]?.ToString();
                if (!string.IsNullOrEmpty(selectedItem) && int.TryParse(selectedItem, out int selectedValue) && selectedValue > 0)
                {
                    return Math.Min(selectedValue, 10000);
                }
            }
            
            // Если ничего не выбрано, пробуем распарсить введённое значение
            var text = comboBox.Text.Trim();
            if (int.TryParse(text, out int value) && value > 0)
                return Math.Min(value, 10000); // Ограничиваем максимум 10000 Mbps
            
            return 100; // default
        }
        
        private static void SetComboBoxFromMbps(ComboBox comboBox, int mbps)
        {
            // Сначала пытаемся найти значение в списке Items
            var mbpsString = mbps.ToString();
            var foundIndex = -1;
            
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i]?.ToString() == mbpsString)
                {
                    foundIndex = i;
                    break;
                }
            }
            
            if (foundIndex >= 0)
            {
                // Если значение найдено в списке, выбираем его
                comboBox.SelectedIndex = foundIndex;
            }
            else
            {
                // Если значение не найдено, устанавливаем текст напрямую
                comboBox.Text = mbpsString;
            }
        }

        public GlobalSettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            InitializeLanguageComboBox();
            LoadSettings();
            ApplyLocalization();
        }
        
        private void InitializeLanguageComboBox()
        {
            var languages = LocalizationManager.GetSupportedLanguages();
            _languageComboBox.DataSource = languages;
            _languageComboBox.DisplayMember = "NativeName";
            _languageComboBox.ValueMember = "Code";
        }
        
        /// <summary>
        /// Применяет локализацию к элементам формы
        /// </summary>
        private void ApplyLocalization()
        {
            Text = LocalizationManager.GetString("GlobalSettings_Title");
            _maxConnectionsLabel.Text = LocalizationManager.GetString("GlobalSettings_MaxConnections") + ":";
            _maxHalfOpenLabel.Text = LocalizationManager.GetString("GlobalSettings_MaxHalfOpen") + ":";
            _maxPiecesLabel.Text = LocalizationManager.GetString("GlobalSettings_MaxPieces") + ":";
            _maxRequestsLabel.Text = LocalizationManager.GetString("GlobalSettings_MaxRequestsPerPeer") + ":";
            _enableLoggingCheckBox.Text = LocalizationManager.GetString("GlobalSettings_EnableLogging");
            _minimizeToTrayCheckBox.Text = LocalizationManager.GetString("GlobalSettings_MinimizeToTray");
            _autoStartCheckBox.Text = LocalizationManager.GetString("GlobalSettings_AutoStartOnLaunch");
            _autoStartOnAddCheckBox.Text = LocalizationManager.GetString("GlobalSettings_AutoStartOnAdd");
            _copyTorrentFileCheckBox.Text = LocalizationManager.GetString("GlobalSettings_CopyTorrentFile");
            _globalDownloadLimitCheckBox.Text = LocalizationManager.GetString("GlobalSettings_GlobalDownloadSpeed") + ":";
            _globalUploadLimitCheckBox.Text = LocalizationManager.GetString("GlobalSettings_GlobalUploadSpeed") + ":";
            _languageLabel.Text = LocalizationManager.GetString("GlobalSettings_Language") + ":";
            _okButton.Text = LocalizationManager.GetString("GlobalSettings_OK");
            _cancelButton.Text = LocalizationManager.GetString("GlobalSettings_Cancel");
            _infoLabel.Text = LocalizationManager.GetString("GlobalSettings_Info");
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
            
            // Установить значения для ComboBox загрузки
            if (_settings.GlobalMaxDownloadSpeed.HasValue)
            {
                // Конвертируем bytes/sec в Mbps: bytes * 8 / 1,000,000
                // 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
                int mbps = (int)Math.Round(_settings.GlobalMaxDownloadSpeed.Value * 8.0 / 1_000_000.0);
                mbps = Math.Max(1, Math.Min(mbps, 10000)); // Ограничиваем диапазон
                SetComboBoxFromMbps(_globalDownloadLimitComboBox, mbps);
            }
            else
            {
                // Если лимит не установлен, устанавливаем значение по умолчанию (100 Mbps)
                SetComboBoxFromMbps(_globalDownloadLimitComboBox, 100);
            }
            
            // Установить значения для ComboBox отдачи
            if (_settings.GlobalMaxUploadSpeed.HasValue)
            {
                // Конвертируем bytes/sec в Mbps: bytes * 8 / 1,000,000
                // 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
                int mbps = (int)Math.Round(_settings.GlobalMaxUploadSpeed.Value * 8.0 / 1_000_000.0);
                mbps = Math.Max(1, Math.Min(mbps, 10000)); // Ограничиваем диапазон
                SetComboBoxFromMbps(_globalUploadLimitComboBox, mbps);
            }
            else
            {
                // Если лимит не установлен, устанавливаем значение по умолчанию (100 Mbps)
                SetComboBoxFromMbps(_globalUploadLimitComboBox, 100);
            }
            
            // Установить выбранный язык
            if (!string.IsNullOrEmpty(_settings.LanguageCode))
            {
                for (int i = 0; i < _languageComboBox.Items.Count; i++)
                {
                    if (_languageComboBox.Items[i] is LanguageInfo langInfo && langInfo.Code == _settings.LanguageCode)
                    {
                        _languageComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                // Если язык не установлен, выбираем текущий язык системы
                var currentLang = LocalizationManager.GetCurrentLanguage();
                for (int i = 0; i < _languageComboBox.Items.Count; i++)
                {
                    if (_languageComboBox.Items[i] is LanguageInfo langInfo)
                    {
                        // Сравниваем полный код или базовый код (например, "pt-BR" или "pt")
                        if (langInfo.Code == currentLang || 
                            currentLang.StartsWith(langInfo.Code + "-", StringComparison.OrdinalIgnoreCase) ||
                            langInfo.Code.StartsWith(currentLang.Split('-')[0] + "-", StringComparison.OrdinalIgnoreCase))
                        {
                            _languageComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
    }
}
