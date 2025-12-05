using System.Windows.Forms;
using TorrentClient.UI.Interfaces;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace TorrentClient.UI
{
    /// <summary>
    /// Управление иконкой в системном трее (SRP)
    /// </summary>
    public class TrayIconManager : ITrayIconManager
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly Form _form;
        private EventHandler? _trayIconDoubleClickHandler;
        private EventHandler? _trayMenuRestoreHandler;
        private EventHandler? _trayMenuExitHandler;
        private System.Windows.Forms.Timer? _speedUpdateTimer;
        
        private const int TrayBalloonTimeoutMs = 1000;
        private const int SpeedUpdateIntervalMs = 2000;

        public bool Visible
        {
            get => _trayIcon.Visible;
            set => _trayIcon.Visible = value;
        }

        public TrayIconManager(Form form, Action restoreAction, Action exitAction)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
            
            // Создаём именованные обработчики для возможности отписки
            _trayMenuRestoreHandler = (s, e) => restoreAction();
            _trayMenuExitHandler = (s, e) => exitAction();
            
            // Создаём контекстное меню для трея
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Открыть", null, _trayMenuRestoreHandler);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("Выход", null, _trayMenuExitHandler);
            
            // Создаём иконку в трее
            _trayIcon = new NotifyIcon
            {
                Icon = form.Icon ?? SystemIcons.Application,
                Text = "TorrentClient",
                ContextMenuStrip = _trayMenu,
                Visible = false
            };
            
            // Сохраняем ссылку на обработчик для возможности отписки
            _trayIconDoubleClickHandler = (s, e) => restoreAction();
            _trayIcon.DoubleClick += _trayIconDoubleClickHandler;
            
            // Таймер для периодического обновления информации о скорости
            _speedUpdateTimer = new System.Windows.Forms.Timer
            {
                Interval = SpeedUpdateIntervalMs,
                Enabled = true
            };
        }
        
        /// <summary>
        /// Обновляет текст иконки в трее с информацией о скорости
        /// </summary>
        public void UpdateSpeedInfo(long downloadSpeed, long uploadSpeed)
        {
            if (_trayIcon == null || !_trayIcon.Visible)
                return;
                
            var downloadText = FormatSpeed(downloadSpeed);
            var uploadText = FormatSpeed(uploadSpeed);
            
            var tooltipText = $"TorrentClient\n\nЗагрузка: {downloadText}\nОтдача: {uploadText}";
            _trayIcon.Text = tooltipText;
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

        public void MinimizeToTray()
        {
            _form.Hide();
            _trayIcon.Visible = true;
            ShowBalloonTip(TrayBalloonTimeoutMs, "TorrentClient", "Приложение свёрнуто в трей", ToolTipIcon.Info);
        }

        public void RestoreFromTray()
        {
            _form.Show();
            _form.WindowState = FormWindowState.Normal;
            _form.Activate();
            _trayIcon.Visible = false;
        }

        public void ShowBalloonTip(int timeout, string title, string text, ToolTipIcon icon)
        {
            _trayIcon.ShowBalloonTip(timeout, title, text, icon);
        }

        public void Dispose()
        {
            if (_trayIcon != null && _trayIconDoubleClickHandler != null)
            {
                _trayIcon.DoubleClick -= _trayIconDoubleClickHandler;
                _trayIconDoubleClickHandler = null;
            }
            
            _trayMenuRestoreHandler = null;
            _trayMenuExitHandler = null;
            
            _speedUpdateTimer?.Stop();
            _speedUpdateTimer?.Dispose();
            _speedUpdateTimer = null;
            
            _trayIcon?.Dispose();
            _trayMenu?.Dispose();
        }
    }
}

