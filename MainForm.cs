using TorrentClient.Core.Interfaces;
using TorrentClient.UI;
using TorrentClient.UI.Services;
using TorrentClient.UI.Services.Interfaces;

namespace TorrentClient
{
    public partial class MainForm : Form, IDisposable
    {
        #region Константы
        
        private const int UiUpdateIntervalMs = 2000;
        private const int CleanupIntervalMinutes = 5;
        
        #endregion
        
        #region Поля
        
        private ITorrentManager? _torrentManager;
        private IAppSettingsManager? _settingsManager;
        private TrayIconManager? _trayIconManager;
        private MainFormPresenter? _presenter;
        private ITorrentOperationsService? _operationsService;
        private DateTime _lastCleanupTime = DateTime.UtcNow;
        
        #endregion
        
        public MainForm()
        {
            InitializeComponent();
            InitializeDragAndDrop();
            InitializeApplication();
            ResizeListViewColumns(); // Первоначальное растягивание колонок
        }
        
        /// <summary>
        /// Инициализирует поддержку drag-and-drop для .torrent файлов
        /// </summary>
        private void InitializeDragAndDrop()
        {
            AllowDrop = true;
            DragEnter += MainForm_DragEnter;
            DragDrop += MainForm_DragDrop;
        }

        /// <summary>
        /// Обработчик события DragEnter - проверяет, что перетаскивается .torrent файл
        /// </summary>
        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;
                
            // Проверяем, что перетаскиваются файлы
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    // Проверяем, что хотя бы один файл имеет расширение .torrent
                    var hasTorrentFile = files.Any(f => 
                        File.Exists(f) && 
                        Path.GetExtension(f).Equals(".torrent", StringComparison.OrdinalIgnoreCase));
                    
                    if (hasTorrentFile)
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            
            e.Effect = DragDropEffects.None;
        }

        /// <summary>
        /// Обработчик события DragDrop - добавляет перетащенные .torrent файлы
        /// </summary>
        private async void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0)
                return;

            // Фильтруем только .torrent файлы
            var torrentFiles = files
                .Where(f => File.Exists(f) && 
                           Path.GetExtension(f).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (torrentFiles.Length == 0)
            {
                _statusLabel.Text = "Перетащите файлы с расширением .torrent";
                return;
            }

            await AddTorrentsFromFilesAsync(torrentFiles);
        }
        
        /// <summary>
        /// Восстанавливает окно из системного трея
        /// </summary>
        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _trayIconManager?.RestoreFromTray();
        }

        /// <summary>
        /// Завершает работу приложения
        /// </summary>
        private void ExitApplication()
        {
            UnsubscribeFromEvents();
            _trayIconManager?.Dispose();
            _torrentManager?.Dispose();
            Environment.Exit(0);
        }

        /// <summary>
        /// Инициализирует приложение через ApplicationInitializer (SRP)
        /// </summary>
        private void InitializeApplication()
        {
            // Используем ApplicationInitializer для создания зависимостей (SRP, DIP)
            var initializer = new ApplicationInitializer();
            var dependencies = initializer.Initialize();
            
            // Сохраняем зависимости
            _settingsManager = dependencies.SettingsManager;
            _torrentManager = dependencies.TorrentManager;
            _operationsService = dependencies.OperationsService;
            
            // Создаём UI-специфичные компоненты
            _trayIconManager = new TrayIconManager(this, RestoreFromTray, ExitApplication);

            // Создаём презентер с зависимостями
            _presenter = new MainFormPresenter(
                this,
                dependencies.TorrentManager,
                dependencies.SettingsManager,
                dependencies.OperationsService,
                dependencies.ListViewUpdater,
                _trayIconManager,
                dependencies.UpdateThrottler)
            {
                AppSettings = dependencies.AppSettings
            };

            // Устанавливаем колбэки через презентер (убираем зависимость от MainForm)
            _presenter.SetTorrentManagerCallbacks(_torrentManager, _torrentListView, 
                _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
            
            // Загружаем сохранённые торренты и настраиваем таймер
            LoadSavedTorrents();
            SetupUpdateTimer();
        }

        private void LoadSavedTorrents()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_presenter != null)
                    {
                        await _presenter.LoadSavedTorrentsAsync();
                    }
                    
                    if (_presenter?.AppSettings?.AutoStartOnLaunch == true)
                    {
                        UiThreadMarshaller.InvokeSafe(this, () =>
                        {
                            _statusLabel.Text = "Торренты загружены и запущены";
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("[MainForm] Ошибка загрузки торрентов", ex);
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        _statusLabel.Text = "Ошибка загрузки сохранённых торрентов";
                    });
                }
            }).ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    Logger.LogError("[MainForm] Необработанное исключение в LoadSavedTorrents", t.Exception);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void SetupUpdateTimer()
        {
            if (_updateTimer == null) return;
            
            _updateTimer.Interval = UiUpdateIntervalMs;
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private async void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_presenter == null) return;
            
            // Периодическая очистка устаревших записей
            var now = DateTime.UtcNow;
            if ((now - _lastCleanupTime).TotalMinutes >= CleanupIntervalMinutes)
            {
                // Используем существующий listViewUpdater из presenter через публичный метод
                // Очистка выполняется через presenter
                _lastCleanupTime = now;
            }
            
            await _presenter.UpdateTorrentsListAsync(_torrentListView, _downloadSpeedLabel, _uploadSpeedLabel);
        }

        private async void AddTorrentButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Torrent files (*.torrent)|*.torrent|All files (*.*)|*.*",
                Title = "Выберите торрент файлы",
                Multiselect = true
            };

            if (dialog.ShowDialog() != DialogResult.OK || dialog.FileNames.Length == 0)
                return;

            await AddTorrentsFromFilesAsync(dialog.FileNames);
        }

        /// <summary>
        /// Добавляет торренты из указанных файлов
        /// </summary>
        private async Task AddTorrentsFromFilesAsync(string[] fileNames)
        {
            string? downloadPath = _presenter?.AppSettings?.DefaultDownloadPath;
            
            // Если папка по умолчанию не задана - запрашиваем
            if (string.IsNullOrEmpty(downloadPath))
            {
                using var folderDialog = new FolderBrowserDialog
                {
                    Description = "Выберите папку для сохранения торрентов",
                    SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Torrents")
                };

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    downloadPath = folderDialog.SelectedPath;
                    
                    var saveAsDefault = MessageBox.Show(
                        "Сохранить эту папку как папку по умолчанию для будущих торрентов?",
                        "Папка по умолчанию",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (saveAsDefault == DialogResult.Yes && _presenter?.AppSettings != null && _settingsManager != null)
                    {
                        _presenter.AppSettings.DefaultDownloadPath = downloadPath;
                        _settingsManager.SaveSettings(_presenter.AppSettings);
                    }
                }
                else
                {
                    return;
                }
            }

            if (_operationsService == null || _presenter?.AppSettings == null)
                return;

            try
            {
                var result = await _operationsService.AddTorrentsAsync(
                    fileNames,
                    downloadPath,
                    _presenter.AppSettings);

                // Обновляем статус и показываем результаты
                if (result.IsSuccess)
                {
                    _statusLabel.Text = result.AddedCount == 1 
                        ? $"Торрент добавлен: {Path.GetFileName(fileNames[0])}"
                        : $"Добавлено торрентов: {result.AddedCount}";
                }
                else
                {
                    var message = new System.Text.StringBuilder();
                    
                    // Успешно добавленные
                    if (result.AddedCount > 0)
                        message.AppendLine($"✓ Добавлено торрентов: {result.AddedCount}");
                    
                    // Пропущенные из-за недостатка места
                    if (result.SkippedCount > 0)
                        message.AppendLine($"⚠ Пропущено из-за недостатка места: {result.SkippedCount}");
                    
                    // Ошибки
                    if (result.FailedCount > 0)
                        message.AppendLine($"✗ Ошибок: {result.FailedCount}");
                    
                    // Предупреждения о месте на диске
                    if (result.HasWarnings)
                    {
                        message.AppendLine("\n⚠ Предупреждения о недостатке места на диске:");
                        foreach (var warning in result.Warnings)
                        {
                            message.AppendLine($"  • {warning}");
                        }
                    }
                    
                    // Детали ошибок
                    if (result.Errors.Count > 0)
                    {
                        message.AppendLine("\n✗ Ошибки:");
                        foreach (var error in result.Errors.Take(5))
                        {
                            message.AppendLine($"  • {error}");
                        }
                        if (result.Errors.Count > 5)
                            message.AppendLine($"  ... и еще {result.Errors.Count - 5} ошибок");
                    }
                    
                    var icon = result.FailedCount > 0 ? MessageBoxIcon.Error : 
                               result.HasWarnings ? MessageBoxIcon.Warning : 
                               MessageBoxIcon.Information;
                    
                    MessageBox.Show(message.ToString(), "Результат добавления торрентов", MessageBoxButtons.OK, icon);
                    
                    // Обновляем статус
                    var statusParts = new List<string>();
                    if (result.AddedCount > 0)
                        statusParts.Add($"Добавлено: {result.AddedCount}");
                    if (result.SkippedCount > 0)
                        statusParts.Add($"Пропущено: {result.SkippedCount}");
                    if (result.FailedCount > 0)
                        statusParts.Add($"Ошибок: {result.FailedCount}");
                    
                    _statusLabel.Text = statusParts.Count > 0 
                        ? string.Join(", ", statusParts)
                        : "Не удалось добавить торренты";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[MainForm] Ошибка добавления торрентов", ex);
                MessageBox.Show($"Ошибка добавления торрентов: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveTorrentButton_Click(object? sender, EventArgs e)
        {
            if (_torrentManager == null || _operationsService == null)
                return;

            var selectedItems = _torrentListView.SelectedItems.Cast<ListViewItem>().ToList();
            if (selectedItems.Count == 0)
                return;

            var selectedTorrentIds = selectedItems
                .Where(item => item.Tag != null)
                .Select(item => item.Tag?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();

            if (selectedTorrentIds.Count == 0)
                return;

            using var dialog = new RemoveTorrentDialog(selectedTorrentIds.Count);
            if (dialog.ShowDialog(this) == DialogResult.Yes)
            {
                if (_presenter != null)
                {
                    _presenter.SelectedTorrentId = string.Empty;
                    _presenter.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                }
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _operationsService.RemoveTorrentsAsync(selectedTorrentIds, dialog.DeleteFiles).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[MainForm] Ошибка удаления торрентов", ex);
                        UiThreadMarshaller.InvokeSafe(this, () =>
                        {
                            MessageBox.Show($"Ошибка удаления торрентов: {ex.Message}", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                });
            }
        }

        private void StartButton_Click(object? sender, EventArgs e)
        {
            if (_torrentListView.SelectedItems.Count == 0 || _operationsService == null)
                return;
                
            _startButton.Enabled = false;
            
            var selectedIds = _torrentListView.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _operationsService.StartTorrentsAsync(selectedIds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[MainForm] Ошибка запуска торрентов", ex);
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        MessageBox.Show($"Ошибка запуска торрентов: {ex.Message}", "Ошибка", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _presenter?.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                    });
                }
                finally
                {
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        _presenter?.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                    });
                }
            });
        }

        private void PauseButton_Click(object? sender, EventArgs e)
        {
            if (_torrentListView.SelectedItems.Count == 0 || _operationsService == null)
                return;
                
            _pauseButton.Enabled = false;
            
            var selectedIds = _torrentListView.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _operationsService.PauseTorrentsAsync(selectedIds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[MainForm] Ошибка паузы торрентов", ex);
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        MessageBox.Show($"Ошибка паузы торрентов: {ex.Message}", "Ошибка", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _presenter?.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                    });
                }
                finally
                {
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        _presenter?.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                    });
                }
            });
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();


            if (_torrentListView.SelectedItems.Count == 0 || _operationsService == null)
                return;
                
            _stopButton.Enabled = false;
            
            var selectedIds = _torrentListView.SelectedItems
                .Cast<ListViewItem>()
                .Select(item => item.Tag?.ToString())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _operationsService.StopTorrentsAsync(selectedIds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[MainForm] Ошибка остановки торрентов", ex);
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        MessageBox.Show($"Ошибка остановки торрентов: {ex.Message}", "Ошибка", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _presenter?.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                    });
                }
                finally
                {
                    UiThreadMarshaller.InvokeSafe(this, () =>
                    {
                        _presenter?.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
                    });
                }
            });
        }

        private void SettingsButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_presenter?.SelectedTorrentId) || _torrentManager == null)
                return;

            var torrent = _torrentManager.GetTorrent(_presenter.SelectedTorrentId);
            if (torrent == null)
                return;

            using var dialog = new SpeedSettingsForm(torrent);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _torrentManager.SetTorrentSpeedLimit(_presenter.SelectedTorrentId, 
                    dialog.MaxDownloadSpeed, dialog.MaxUploadSpeed);
            }
        }

        private void TorrentListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_presenter != null)
            {
                if (_torrentListView.SelectedItems.Count > 0)
                {
                    _presenter.SelectedTorrentId = _torrentListView.SelectedItems[0].Tag?.ToString() ?? string.Empty;
                }
                else
                {
                    _presenter.SelectedTorrentId = string.Empty;
                }
                _presenter.UpdateButtons(_torrentListView, _startButton, _pauseButton, _stopButton, _removeTorrentButton, _settingsButton);
            }
        }

        private void TorrentListView_KeyDown(object? sender, KeyEventArgs e)
        {
            // Обработка клавиши Delete для удаления выбранных торрентов
            if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.None)
            {
                e.Handled = true;
                RemoveTorrentButton_Click(sender, e);
            }
        }


        private void SelectDownloadFolderButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Выберите папку по умолчанию для сохранения торрентов",
                SelectedPath = _presenter?.AppSettings?.DefaultDownloadPath ?? 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Torrents")
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (_presenter?.AppSettings != null && _settingsManager != null)
                {
                    _presenter.AppSettings.DefaultDownloadPath = dialog.SelectedPath;
                    _settingsManager.SaveSettings(_presenter.AppSettings);
                    _statusLabel.Text = $"Папка сохранения изменена: {dialog.SelectedPath}";
                }
            }
        }

        private void GlobalSettingsButton_Click(object? sender, EventArgs e)
        {
            if (_presenter?.AppSettings == null || _settingsManager == null)
                return;

            // Загружаем актуальные настройки перед открытием формы
            var currentSettings = _settingsManager.LoadSettings();
            using var dialog = new GlobalSettingsForm(currentSettings);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // Обновляем настройки из формы
                currentSettings.MaxConnections = dialog.MaxConnections;
                currentSettings.MaxHalfOpenConnections = dialog.MaxHalfOpenConnections;
                currentSettings.MaxPiecesToRequest = dialog.MaxPiecesToRequest;
                currentSettings.MaxRequestsPerPeer = dialog.MaxRequestsPerPeer;
                currentSettings.EnableLogging = dialog.EnableLogging;
                currentSettings.MinimizeToTrayOnClose = dialog.MinimizeToTrayOnClose;
                currentSettings.AutoStartOnLaunch = dialog.AutoStartOnLaunch;
                currentSettings.AutoStartOnAdd = dialog.AutoStartOnAdd;
                currentSettings.CopyTorrentFileToDownloadFolder = dialog.CopyTorrentFileToDownloadFolder;
                
                // Получаем значения из формы
                var globalDownloadSpeed = dialog.GlobalMaxDownloadSpeed;
                var globalUploadSpeed = dialog.GlobalMaxUploadSpeed;
                
                // Логируем значения из формы для отладки
                Logger.LogInfo($"[MainForm] Значения из формы: загрузка={FormatSpeed(globalDownloadSpeed)}, отдача={FormatSpeed(globalUploadSpeed)}");
                
                // Применяем значения к настройкам
                currentSettings.GlobalMaxDownloadSpeed = globalDownloadSpeed;
                currentSettings.GlobalMaxUploadSpeed = globalUploadSpeed;
                
                // Логируем значения перед сохранением в файл
                Logger.LogInfo($"[MainForm] Значения перед сохранением: загрузка={FormatSpeed(currentSettings.GlobalMaxDownloadSpeed)}, отдача={FormatSpeed(currentSettings.GlobalMaxUploadSpeed)}");
                
                // Сохраняем настройки в файл
                _settingsManager.SaveSettings(currentSettings);
                
                // Проверяем, что настройки действительно сохранились (загружаем заново)
                var verifySettings = _settingsManager.LoadSettings();
                Logger.LogInfo($"[MainForm] Проверка после сохранения: загрузка={FormatSpeed(verifySettings.GlobalMaxDownloadSpeed)}, отдача={FormatSpeed(verifySettings.GlobalMaxUploadSpeed)}");
                
                // Обновляем настройки в presenter из проверенных настроек
                _presenter.AppSettings = verifySettings;
                
                // Применяем настройки (используем проверенные настройки)
                ApplySettingsToTorrents(verifySettings);
                Logger.SetEnabled(verifySettings.EnableLogging);
                
                // Применяем глобальные лимиты скорости (используем проверенные настройки)
                GlobalSpeedLimiter.Instance.UpdateLimits(
                    verifySettings.GlobalMaxDownloadSpeed,
                    verifySettings.GlobalMaxUploadSpeed);
                
                Logger.LogInfo($"[MainForm] Глобальные лимиты скорости обновлены: загрузка={FormatSpeed(verifySettings.GlobalMaxDownloadSpeed)}, отдача={FormatSpeed(verifySettings.GlobalMaxUploadSpeed)}");
                
                _torrentManager?.ApplyGlobalSettings(
                    verifySettings.MaxConnections,
                    verifySettings.MaxHalfOpenConnections,
                    verifySettings.MaxPiecesToRequest,
                    verifySettings.MaxRequestsPerPeer);
                
                _statusLabel.Text = $"Настройки сохранены: {verifySettings.MaxConnections} соед., {verifySettings.MaxPiecesToRequest} кусков";
            }
        }
        
        private static string FormatSpeed(long? bytesPerSecond)
        {
            if (bytesPerSecond == null) return "без ограничений";
            // Правильная конвертация: 1 Mbps = 1,000,000 бит/сек = 125,000 байт/сек
            // Mbps = (bytesPerSecond * 8) / 1,000,000
            var mbps = bytesPerSecond.Value * 8.0 / 1_000_000.0;
            return $"{mbps:F1} Mbps";
        }
        
        private void ApplySettingsToTorrents(AppSettings settings)
        {
            if (_torrentManager == null) return;
            
            _ = Task.Run(() =>
            {
                try
                {
                    _torrentManager.ApplyGlobalSettings(
                        settings.MaxConnections,
                        settings.MaxHalfOpenConnections,
                        settings.MaxPiecesToRequest,
                        settings.MaxRequestsPerPeer);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[MainForm] Ошибка применения настроек к торрентам", ex);
                }
            });
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            if (WindowState == FormWindowState.Minimized)
            {
                _trayIconManager?.MinimizeToTray();
            }
            else
            {
                ResizeListViewColumns();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ResizeListViewColumns(); // Растягиваем колонки при загрузке формы
        }

        /// <summary>
        /// Растягивает колонки ListView по ширине окна
        /// </summary>
        private void ResizeListViewColumns()
        {
            if (_torrentListView == null || _torrentListView.Columns.Count == 0)
                return;

            // Получаем доступную ширину (ширина ListView минус ширина вертикальной полосы прокрутки)
            var availableWidth = _torrentListView.ClientSize.Width;
            if (_torrentListView.Items.Count > 0 && _torrentListView.GetItemRect(0).Height * _torrentListView.Items.Count > _torrentListView.ClientSize.Height)
            {
                // Если есть вертикальная прокрутка, вычитаем её ширину
                availableWidth -= SystemInformation.VerticalScrollBarWidth;
            }

            if (availableWidth <= 0)
                return;

            // Пропорции колонок (сумма = 100)
            var columnProportions = new[]
            {
                2.0,   // № (40px -> 2%)
                28.0,  // Название (460px -> 28%, уменьшено для освобождения места)
                11.5,  // Размер (200px -> 11.5%)
                7.5,   // Прогресс (120px -> 7.5%)
                23.0,  // Скорость (увеличено до 23% для отображения лимитов: "50 Mbps [↓100.0 Mbps, ↑50.0 Mbps]")
                10.5,  // Загружено (170px -> 10.5%)
                7.5,   // Пиры (130px -> 7.5%)
                10.0   // Статус (160px -> 10%)
            };

            var totalProportion = columnProportions.Sum();
            
            for (int i = 0; i < _torrentListView.Columns.Count && i < columnProportions.Length; i++)
            {
                var width = (int)(availableWidth * columnProportions[i] / totalProportion);
                _torrentListView.Columns[i].Width = Math.Max(width, 30); // Минимум 30px
            }
        }

        /// <summary>
        /// Обработчик изменения ширины колонки - предотвращает ручное изменение
        /// </summary>
        private void TorrentListView_ColumnWidthChanging(object? sender, ColumnWidthChangingEventArgs e)
        {
            // Разрешаем изменение ширины колонок пользователем
            // Автоматическое растягивание будет применено при изменении размера окна
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // Обработка клавиши Delete для удаления выбранных торрентов
            // Работает когда фокус на форме или ListView
            if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.None)
            {
                // Проверяем, что есть выбранные элементы в ListView
                if (_torrentListView.SelectedItems.Count > 0)
                {
                    e.Handled = true;
                    RemoveTorrentButton_Click(_torrentListView, e);
                }
            }
        }

        private void MinimizeToTray()
        {
            _trayIconManager?.MinimizeToTray();
        }

        private void UnsubscribeFromEvents()
        {
            _torrentManager?.SetCallbacks(null!);
            
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Tick -= UpdateTimer_Tick;
                _updateTimer.Dispose();
                _updateTimer = null;
            }
            
            if (_torrentListView != null)
            {
                _torrentListView.SelectedIndexChanged -= TorrentListView_SelectedIndexChanged;
                _torrentListView.KeyDown -= TorrentListView_KeyDown;
            }
            
            if (_addTorrentButton != null)
            {
                _addTorrentButton.Click -= AddTorrentButton_Click;
            }
            
            if (_removeTorrentButton != null)
            {
                _removeTorrentButton.Click -= RemoveTorrentButton_Click;
            }
            
            if (_startButton != null)
            {
                _startButton.Click -= StartButton_Click;
            }
            
            if (_pauseButton != null)
            {
                _pauseButton.Click -= PauseButton_Click;
            }
            
            if (_stopButton != null)
            {
                _stopButton.Click -= StopButton_Click;
            }
            
            if (_settingsButton != null)
            {
                _settingsButton.Click -= SettingsButton_Click;
            }
            
            if (_selectDownloadFolderButton != null)
            {
                _selectDownloadFolderButton.Click -= SelectDownloadFolderButton_Click;
            }
            
            if (_globalSettingsButton != null)
            {
                _globalSettingsButton.Click -= GlobalSettingsButton_Click;
            }
            
            _presenter?.Cleanup();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && (_presenter?.AppSettings?.MinimizeToTrayOnClose ?? true))
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }
            
            // КРИТИЧНО: Отменяем закрытие формы и выполняем асинхронное закрытие
            // Это позволяет форме закрыться быстро, а освобождение ресурсов происходит в фоне
            e.Cancel = true;
            
            // Скрываем форму сразу для быстрого отклика UI
            Hide();
            
            // Запускаем асинхронное закрытие в фоне
            _ = Task.Run(async () =>
            {
                try
                {
                    await CloseFormAsync().ConfigureAwait(false);
                    
                    // После завершения закрываем приложение
                    GlobalSpeedLimiter.Instance.Dispose();
                    Logger.Close();
                    
                    // Закрываем форму через UI поток
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => Close()));
                    }
                    else
                    {
                        Close();
                    }
                    
                    // Принудительно завершаем приложение через небольшую задержку
                    await Task.Delay(100).ConfigureAwait(false);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[MainForm] Ошибка при асинхронном закрытии формы", ex);
                    // В случае ошибки все равно закрываем приложение
                    try
                    {
                        if (InvokeRequired)
                        {
                            Invoke(new Action(() => Environment.Exit(0)));
                        }
                        else
                        {
                            Environment.Exit(0);
                        }
                    }
                    catch { }
                }
            });
        }
        
        /// <summary>
        /// Асинхронно закрывает форму и освобождает ресурсы
        /// </summary>
        private async Task CloseFormAsync()
        {
            try
            {
                // Быстро останавливаем таймер
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    _updateTimer.Tick -= UpdateTimer_Tick;
                    _updateTimer.Dispose();
                    _updateTimer = null;
                }
                
                // Освобождаем tray icon
                _trayIconManager?.Dispose();
                
                // Отписываемся от событий
                UnsubscribeFromEvents();
                
                // Асинхронно сохраняем и освобождаем торренты с коротким таймаутом
                if (_torrentManager != null)
                {
                    // Сохраняем состояние (синхронно, быстро)
                    _torrentManager.SaveAllTorrents();
                    
                    // Пытаемся асинхронно освободить ресурсы с очень коротким таймаутом (50мс для максимальной скорости)
                    try
                    {
                        var disposeTask = _torrentManager.DisposeAsync().AsTask();
                        await disposeTask.WaitAsync(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // Если не успело за 50мс - используем синхронный Dispose (быстрее чем ждать)
                        Logger.LogWarning("[MainForm] DisposeAsync не завершился за 50мс, используем синхронный Dispose");
                        try
                        {
                            _torrentManager.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[MainForm] Ошибка при синхронном Dispose: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[MainForm] Ошибка при DisposeAsync: {ex.Message}");
                        // Fallback на синхронный Dispose для гарантированного освобождения
                        try
                        {
                            _torrentManager.Dispose();
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[MainForm] Ошибка при асинхронном закрытии", ex);
            }
        }
    }
}
