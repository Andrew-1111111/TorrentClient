using TorrentClient.Core.Interfaces;
using TorrentClient.UI.Services;

namespace TorrentClient.Core
{
    /// <summary>
    /// Обёртка колбэков для TorrentManager
    /// Перенаправляет вызовы в MainFormPresenter
    /// </summary>
    internal class TorrentManagerCallbacksWrapper : ITorrentManagerCallbacks
    {
        private readonly MainFormPresenter _presenter;
        private readonly System.Windows.Forms.ListView _torrentListView;
        private readonly System.Windows.Forms.Button? _startButton;
        private readonly System.Windows.Forms.Button? _pauseButton;
        private readonly System.Windows.Forms.Button? _stopButton;
        private readonly System.Windows.Forms.Button? _removeButton;
        private readonly System.Windows.Forms.Button? _settingsButton;

        public TorrentManagerCallbacksWrapper(
            MainFormPresenter presenter, 
            System.Windows.Forms.ListView torrentListView,
            System.Windows.Forms.Button? startButton = null,
            System.Windows.Forms.Button? pauseButton = null,
            System.Windows.Forms.Button? stopButton = null,
            System.Windows.Forms.Button? removeButton = null,
            System.Windows.Forms.Button? settingsButton = null)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _torrentListView = torrentListView ?? throw new ArgumentNullException(nameof(torrentListView));
            _startButton = startButton;
            _pauseButton = pauseButton;
            _stopButton = stopButton;
            _removeButton = removeButton;
            _settingsButton = settingsButton;
        }

        public Task OnTorrentAddedAsync(Models.Torrent torrent)
        {
            _presenter.OnTorrentAdded(_torrentListView, torrent);
            return Task.CompletedTask;
        }

        public Task OnTorrentRemovedAsync(Models.Torrent torrent)
        {
            _presenter.OnTorrentRemoved(_torrentListView, torrent);
            return Task.CompletedTask;
        }

        public Task OnTorrentUpdatedAsync(Models.Torrent torrent)
        {
            _presenter.OnTorrentUpdated(_torrentListView, torrent);
            
            // Обновляем кнопки, если это выбранный торрент
            if (torrent.Id == _presenter.SelectedTorrentId && 
                _startButton != null && _pauseButton != null && _stopButton != null && 
                _removeButton != null && _settingsButton != null)
            {
                _presenter.UpdateButtons(_torrentListView, _startButton, _pauseButton, 
                    _stopButton, _removeButton, _settingsButton);
            }
            
            return Task.CompletedTask;
        }
    }
}

