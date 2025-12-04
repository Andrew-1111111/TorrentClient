namespace TorrentClient.Protocol
{
    /// <summary>
    /// Менеджер блокировки - управление choke/unchoke (алгоритм Tit-for-Tat)
    /// </summary>
    public class ChokeManager : IDisposable
    {
        #region Константы

        private const int ChokeInterval = 10;           // Интервал проверки (сек)
        private const int OptimisticUnchokeSlots = 1;   // Слоты оптимистичной разблокировки
        private const int MaxUnchoked = 4;              // Максимум разблокированных пиров

        #endregion

        #region Поля

        private readonly List<PeerConnection> _peers = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private Task? _chokeTask;

        #endregion

        #region Публичные методы

        /// <summary>
        /// Запускает менеджер
        /// </summary>
        public void Start() => 
            _chokeTask = Task.Run(() => ChokeLoopAsync(_cts.Token));

        /// <summary>
        /// Останавливает менеджер
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            _chokeTask?.Wait(TimeSpan.FromSeconds(1));
            // КРИТИЧНО: Обнуляем задачу для предотвращения утечки памяти
            _chokeTask = null;
        }

        /// <summary>
        /// Добавляет пира
        /// </summary>
        public void AddPeer(PeerConnection peer)
        {
            _lock.Wait();
            try
            {
                if (!_peers.Contains(peer))
                    _peers.Add(peer);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Удаляет пира
        /// </summary>
        public void RemovePeer(PeerConnection peer)
        {
            _lock.Wait();
            try
            {
                _peers.Remove(peer);
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            Stop();
            
            // Очищаем список пиров для освобождения ссылок
            _lock.Wait();
            try
            {
                _peers.Clear();
            }
            finally
            {
                _lock.Release();
            }
            
            _cts.Dispose();
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Приватные методы

        private async Task ChokeLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await UpdateChokeStateAsync();
                    await Task.Delay(TimeSpan.FromSeconds(ChokeInterval), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка в цикле ChokeManager", ex);
                }
            }
        }

        private async Task UpdateChokeStateAsync()
        {
            await _lock.WaitAsync();
            List<PeerConnection> connectedPeers;
            try
            {
                connectedPeers = _peers
                    .Where(p => p.IsConnected && p.PeerInterested)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }

            if (connectedPeers.Count == 0)
                return;

            // Сортировка по скорости загрузки (Tit-for-Tat)
            var sortedPeers = connectedPeers
                .OrderByDescending(p => p.DownloadSpeed)
                .ToList();

            int unchokedCount = 0;
            PeerConnection? optimisticUnchoke = null;

            foreach (var peer in sortedPeers)
            {
                if (unchokedCount < MaxUnchoked - OptimisticUnchokeSlots)
                {
                    // Разблокировка на основе производительности
                    if (peer.PeerChoked)
                    {
                        await peer.SendUnchokeAsync();
                        unchokedCount++;
                        Logger.LogInfo($"Разблокирован пир {peer.EndPoint} (скорость: {peer.DownloadSpeed} байт/с)");
                    }
                }
                else if (unchokedCount < MaxUnchoked && optimisticUnchoke == null)
                {
                    // Оптимистичная разблокировка - шанс для нового пира
                    if (peer.PeerChoked)
                    {
                        await peer.SendUnchokeAsync();
                        optimisticUnchoke = peer;
                        unchokedCount++;
                        Logger.LogInfo($"Оптимистичная разблокировка: {peer.EndPoint}");
                    }
                }
                else
                {
                    // Блокировка остальных
                    if (!peer.PeerChoked)
                    {
                        await peer.SendChokeAsync();
                        Logger.LogInfo($"Заблокирован пир {peer.EndPoint}");
                    }
                }
            }
        }

        #endregion
    }
}