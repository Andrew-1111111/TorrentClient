using System.Windows.Forms;
using TorrentClient.Core;
using TorrentClient.Core.Interfaces;
using TorrentClient.UI.Interfaces;
using TorrentClient.UI.Services;
using TorrentClient.UI.Services.Interfaces;

namespace TorrentClient.UI.Services
{
    /// <summary>
    /// Presenter для MainForm - разделяет бизнес-логику и UI
    /// </summary>
    public class MainFormPresenter
    {
        private readonly ITorrentManager _torrentManager;
        private readonly IAppSettingsManager _settingsManager;
        private readonly ITorrentOperationsService _operationsService;
        private readonly ITorrentListViewUpdater _listViewUpdater;
        private readonly ITrayIconManager _trayIconManager;
        private readonly IUpdateThrottler _updateThrottler;
        private readonly Control _mainForm;

        private AppSettings? _appSettings;
        private string _selectedTorrentId = string.Empty;
        private readonly HashSet<string> _pendingUpdates = [];
        private volatile bool _isUpdating = false;
        
        private TorrentManagerCallbacksWrapper? _callbacksWrapper;

        public MainFormPresenter(
            Control mainForm,
            ITorrentManager torrentManager,
            IAppSettingsManager settingsManager,
            ITorrentOperationsService operationsService,
            ITorrentListViewUpdater listViewUpdater,
            ITrayIconManager trayIconManager,
            IUpdateThrottler updateThrottler)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _torrentManager = torrentManager ?? throw new ArgumentNullException(nameof(torrentManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _operationsService = operationsService ?? throw new ArgumentNullException(nameof(operationsService));
            _listViewUpdater = listViewUpdater ?? throw new ArgumentNullException(nameof(listViewUpdater));
            _trayIconManager = trayIconManager ?? throw new ArgumentNullException(nameof(trayIconManager));
            _updateThrottler = updateThrottler ?? throw new ArgumentNullException(nameof(updateThrottler));
        }

        public AppSettings? AppSettings
        {
            get => _appSettings;
            set => _appSettings = value;
        }

        public string SelectedTorrentId
        {
            get => _selectedTorrentId;
            set => _selectedTorrentId = value;
        }

        /// <summary>
        /// Инициализация настроек
        /// </summary>
        public void InitializeSettings()
        {
            _appSettings = _settingsManager.LoadSettings();
            Logger.SetEnabled(_appSettings.EnableLogging);
            GlobalSpeedLimiter.Instance.UpdateLimits(
                _appSettings.GlobalMaxDownloadSpeed,
                _appSettings.GlobalMaxUploadSpeed);
        }

        /// <summary>
        /// Загружает сохранённые торренты
        /// </summary>
        public async Task LoadSavedTorrentsAsync()
        {
            try
            {
                await _operationsService.LoadSavedTorrentsAsync(_appSettings).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError("[MainFormPresenter] Ошибка загрузки торрентов", ex);
                UiThreadMarshaller.InvokeSafe(_mainForm, () =>
                {
                    // Обновление статуса будет выполнено через UI
                });
            }
        }

        /// <summary>
        /// Обновляет список торрентов в UI
        /// </summary>
        public async Task UpdateTorrentsListAsync(ListView torrentListView, ToolStripStatusLabel downloadSpeedLabel, ToolStripStatusLabel uploadSpeedLabel)
        {
            if (_isUpdating || _torrentManager == null)
                return;

            _isUpdating = true;
            try
            {
                await Task.Run(() => _torrentManager.SyncAllTorrentsState()).ConfigureAwait(false);
                var torrents = await Task.Run(() => _torrentManager.GetAllTorrents()).ConfigureAwait(false);

                UiThreadMarshaller.InvokeSafeWithKey(_mainForm, "UpdateTimer", () =>
                {
                    long totalDownloadSpeed = 0;
                    long totalUploadSpeed = 0;

                    torrentListView.BeginUpdate();
                    try
                    {
                        _listViewUpdater.UpdateTorrentsList(torrentListView, torrents,
                            out totalDownloadSpeed, out totalUploadSpeed);
                    }
                    finally
                    {
                        torrentListView.EndUpdate();
                    }

                    UpdateSpeedLabels(downloadSpeedLabel, uploadSpeedLabel, totalDownloadSpeed, totalUploadSpeed);
                    
                    // Обновляем информацию о скорости в трее
                    _trayIconManager.UpdateSpeedInfo(totalDownloadSpeed, totalUploadSpeed);
                }, _pendingUpdates);
            }
            catch (Exception ex)
            {
                Logger.LogError("[MainFormPresenter] Ошибка в UpdateTimer_Tick", ex);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Обновляет кнопки управления
        /// </summary>
        public void UpdateButtons(ListView torrentListView, Button startButton, Button pauseButton, 
            Button stopButton, Button removeButton, Button settingsButton)
        {
            UiThreadMarshaller.InvokeSafeWithKey(_mainForm, "UpdateButtons", () =>
            {
                UpdateButtonsInternal(torrentListView, startButton, pauseButton, stopButton, removeButton, settingsButton);
            }, _pendingUpdates);
        }

        private void UpdateButtonsInternal(ListView torrentListView, Button startButton, Button pauseButton,
            Button stopButton, Button removeButton, Button settingsButton)
        {
            var selectedCount = torrentListView.SelectedItems.Count;
            var hasSelection = selectedCount > 0;
            removeButton.Enabled = hasSelection;
            settingsButton.Enabled = selectedCount == 1 && !string.IsNullOrEmpty(_selectedTorrentId);

            if (!hasSelection || _torrentManager == null)
            {
                startButton.Enabled = false;
                pauseButton.Enabled = false;
                stopButton.Enabled = false;
                return;
            }

            bool canStart = false;
            bool canPause = false;
            bool canStop = false;

            foreach (ListViewItem item in torrentListView.SelectedItems)
            {
                var id = item.Tag?.ToString();
                if (string.IsNullOrEmpty(id))
                    continue;

                var torrent = _torrentManager.GetTorrent(id);
                if (torrent == null)
                    continue;

                if (torrent.State == TorrentState.Stopped ||
                    torrent.State == TorrentState.Paused ||
                    torrent.State == TorrentState.Error)
                {
                    canStart = true;
                }

                if (torrent.State == TorrentState.Downloading)
                {
                    canPause = true;
                }

                if (torrent.State == TorrentState.Downloading ||
                    torrent.State == TorrentState.Paused ||
                    torrent.State == TorrentState.Seeding)
                {
                    canStop = true;
                }
            }

            startButton.Enabled = canStart;
            pauseButton.Enabled = canPause;
            stopButton.Enabled = canStop;
        }

        private static void UpdateSpeedLabels(ToolStripStatusLabel downloadLabel, ToolStripStatusLabel uploadLabel, long downloadSpeed, long uploadSpeed)
        {
            var newDownloadText = $"↓ {FormatSpeed(downloadSpeed)}";
            var newUploadText = $"↑ {FormatSpeed(uploadSpeed)}";
            if (downloadLabel.Text != newDownloadText) downloadLabel.Text = newDownloadText;
            if (uploadLabel.Text != newUploadText) uploadLabel.Text = newUploadText;
        }

        private static string FormatSpeed(long bytesPerSecond)
        {
            // Конвертация согласно стандарту: https://en.wikipedia.org/wiki/Data-rate_units
            // 1 Mbps = 1,000,000 bits/s = 125,000 bytes/s
            // 1 MB/s = 1,000,000 bytes/s = 8,000,000 bits/s
            double mbps = (bytesPerSecond * 8.0) / 1_000_000.0;
            double mbytes = bytesPerSecond / 1_000_000.0;

            if (mbps >= 1000)
            {
                double gbps = mbps / 1000.0;
                double gbytes = bytesPerSecond / 1_000_000_000.0;
                return $"{gbps:0.##} Gbps ({gbytes:0.##} GB/s)";
            }
            else if (mbps >= 1)
            {
                return $"{mbps:0.##} Mbps ({mbytes:0.##} MB/s)";
            }
            else if (mbps >= 0.001)
            {
                double kbps = mbps * 1000.0;
                double kbytes = bytesPerSecond / 1_000.0;
                return $"{kbps:0.#} Kbps ({kbytes:0.#} KB/s)";
            }
            else
            {
                return "0 Mbps (0 MB/s)";
            }
        }

        /// <summary>
        /// Обработчик добавления торрента
        /// </summary>
        public void OnTorrentAdded(ListView torrentListView, Torrent torrent)
        {
            UiThreadMarshaller.InvokeSafeWithKey(_mainForm, $"Added_{torrent.Id}", () =>
            {
                var number = torrentListView.Items.Count + 1;
                var item = new ListViewItem(number.ToString())
                {
                    Tag = torrent.Id
                };
                item.SubItems.Add(torrent.Info.Name);
                item.SubItems.Add(FormatBytes(torrent.Info.TotalSize));
                item.SubItems.Add("0%");
                item.SubItems.Add("0 Mbps");
                item.SubItems.Add("0 B");
                item.SubItems.Add("0/0/0");
                item.SubItems.Add(torrent.State.ToString());

                torrentListView.Items.Add(item);
            }, _pendingUpdates);
        }

        /// <summary>
        /// Обработчик удаления торрента
        /// </summary>
        public void OnTorrentRemoved(ListView torrentListView, Torrent torrent)
        {
            UiThreadMarshaller.InvokeSafeWithKey(_mainForm, $"Removed_{torrent.Id}", () =>
            {
                foreach (ListViewItem item in torrentListView.Items)
                {
                    if (item.Tag?.ToString() == torrent.Id)
                    {
                        torrentListView.Items.Remove(item);
                        break;
                    }
                }

                _updateThrottler.CleanupStaleEntries(new[] { torrent.Id });
            }, _pendingUpdates);
        }

        /// <summary>
        /// Обработчик обновления торрента
        /// </summary>
        public void OnTorrentUpdated(ListView torrentListView, Torrent torrent)
        {
            UiThreadMarshaller.InvokeSafeWithKey(_mainForm, torrent.Id, () =>
            {
                if (_updateThrottler == null || !_updateThrottler.CanUpdate(torrent.Id))
                    return;

                _updateThrottler.MarkUpdated(torrent.Id);

                foreach (ListViewItem item in torrentListView.Items)
                {
                    if (item.Tag?.ToString() == torrent.Id)
                    {
                        _listViewUpdater.UpdateTorrentItem(item, torrent);
                        break;
                    }
                }
                
                // Обновляем кнопки, если это выбранный торрент
                if (torrent.Id == _selectedTorrentId)
                {
                    // Кнопки будут обновлены через UpdateButtons, который вызывается из MainForm
                }
            }, _pendingUpdates);
        }

        private static string FormatBytes(long bytes)
        {
            var sizes = new[] { "B", "KB", "MB", "GB", "TB" };
            var len = (double)bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Устанавливает колбэки для TorrentManager
        /// </summary>
        public void SetTorrentManagerCallbacks(ITorrentManager torrentManager, System.Windows.Forms.ListView torrentListView, 
            System.Windows.Forms.Button? startButton = null, System.Windows.Forms.Button? pauseButton = null,
            System.Windows.Forms.Button? stopButton = null, System.Windows.Forms.Button? removeButton = null,
            System.Windows.Forms.Button? settingsButton = null)
        {
            if (_callbacksWrapper != null)
            {
                torrentManager.SetCallbacks(null!);
                _callbacksWrapper = null;
            }
            
            _callbacksWrapper = new TorrentManagerCallbacksWrapper(this, torrentListView, 
                startButton, pauseButton, stopButton, removeButton, settingsButton);
            torrentManager.SetCallbacks(_callbacksWrapper);
        }

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Cleanup()
        {
            lock (_pendingUpdates)
            {
                _pendingUpdates.Clear();
            }
            
            if (_callbacksWrapper != null)
            {
                _torrentManager?.SetCallbacks(null!);
                _callbacksWrapper = null;
            }
        }
    }
}

