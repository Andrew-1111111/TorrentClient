using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Обёртка колбэков для ActiveTorrent, которая перенаправляет вызовы в Client
    /// </summary>
    internal class ActiveTorrentCallbacksWrapper : IActiveTorrentCallbacks
    {
        private readonly Client _client;
        private readonly Func<ActiveTorrent> _getTorrent;

        public ActiveTorrentCallbacksWrapper(Client client, Func<ActiveTorrent> getTorrent)
        {
            _client = client;
            _getTorrent = getTorrent;
        }

        private static readonly System.Reflection.FieldInfo? _callbacksField = typeof(Client)
            .GetField("_callbacks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public Task OnProgressChangedAsync()
        {
            var torrent = _getTorrent();
            var callbacks = _callbacksField?.GetValue(_client) as ITorrentClientCallbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentProgressAsync(torrent).ConfigureAwait(false));
            }
            return Task.CompletedTask;
        }

        public Task OnCompleteAsync()
        {
            var torrent = _getTorrent();
            var callbacks = _callbacksField?.GetValue(_client) as ITorrentClientCallbacks;
            if (callbacks != null)
            {
                SafeTaskRunner.RunSafe(async () => await callbacks.OnTorrentCompleteAsync(torrent).ConfigureAwait(false));
            }
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(string error)
        {
            // Ошибки обрабатываются локально в ActiveTorrent
            return Task.CompletedTask;
        }
    }
}

