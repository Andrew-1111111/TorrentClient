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
        
        private const int TrayBalloonTimeoutMs = 1000;

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
            
            _trayIcon?.Dispose();
            _trayMenu?.Dispose();
        }
    }
}

