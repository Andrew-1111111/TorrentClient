namespace TorrentClient.Protocol
{
    /// <summary>
    /// Менеджер блокировки - управление choke/unchoke (алгоритм Tit-for-Tat)
    /// </summary>
    public class ChokeManager : IDisposable
    {
        #region Константы

        private const int ChokeInterval = 5;            // Уменьшено для более быстрой переоценки пиров
        private const int OptimisticUnchokeSlots = 8;   // Увеличено для быстрого разгона скорости
        private const int MaxUnchoked = 30;             // Увеличено для максимальной скорости (особенно для одного торрента)

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
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
            _chokeTask?.Wait(TimeSpan.FromSeconds(1));
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
            try
            {
                if (_lock != null)
                {
                    _lock.Wait();
                    try
                    {
                        _peers.Clear();
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Игнорируем, если уже disposed
            }
            
            _cts?.Dispose();
            _lock?.Dispose();
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
            int totalConnectedCount;
            try
            {
                connectedPeers = _peers
                    .Where(p => p.IsConnected && p.PeerInterested)
                    .ToList();
                totalConnectedCount = _peers.Count(p => p.IsConnected);
            }
            finally
            {
                _lock.Release();
            }

            if (connectedPeers.Count == 0)
                return;

            // Динамически увеличиваем MaxUnchoked в зависимости от количества соединений
            // Это позволяет одному торренту использовать больше соединений для максимальной скорости
            // Для одного торрента используем максимально агрессивную стратегию - разблокируем как можно больше пиров
            var dynamicMaxUnchoked = totalConnectedCount > 50 
                ? Math.Min(MaxUnchoked * 2, totalConnectedCount * 4 / 5) // При большом количестве соединений - используем 80%
                : totalConnectedCount > 30
                    ? Math.Min(MaxUnchoked, totalConnectedCount * 3 / 4) // При среднем количестве - используем 75%
                    : totalConnectedCount > 10
                        ? Math.Min(MaxUnchoked, totalConnectedCount) // При малом количестве - используем все доступные
                        : Math.Min(MaxUnchoked, totalConnectedCount); // При очень малом количестве - используем все доступные

            // Сортировка по скорости загрузки (Tit-for-Tat)
            // Приоритет: активные пиры с загрузкой > новые пиры > неактивные
            var sortedPeers = connectedPeers
                .OrderByDescending(p => p.DownloadSpeed > 0 ? 1 : 0) // Сначала активные
                .ThenByDescending(p => p.DownloadSpeed)
                .ToList();

            int unchokedCount = 0;
            var optimisticUnchokes = new List<PeerConnection>();

            foreach (var peer in sortedPeers)
            {
                if (unchokedCount < dynamicMaxUnchoked - OptimisticUnchokeSlots)
                {
                    // Разблокировка на основе производительности (для загрузки)
                    if (peer.PeerChoked)
                    {
                        await peer.SendUnchokeAsync();
                        unchokedCount++;
                        Logger.LogInfo($"Разблокирован пир {peer.EndPoint} (скорость: {peer.DownloadSpeed} байт/с)");
                    }
                }
                else if (unchokedCount < dynamicMaxUnchoked && optimisticUnchokes.Count < OptimisticUnchokeSlots)
                {
                    // Оптимистичная разблокировка - шанс для новых пиров (увеличено количество)
                    if (peer.PeerChoked)
                    {
                        await peer.SendUnchokeAsync();
                        optimisticUnchokes.Add(peer);
                        unchokedCount++;
                        Logger.LogInfo($"Оптимистичная разблокировка: {peer.EndPoint}");
                    }
                }
                else
                {
                    // Блокировка остальных (только если они действительно неактивны)
                    if (!peer.PeerChoked && peer.DownloadSpeed == 0)
                    {
                        await peer.SendChokeAsync();
                        Logger.LogInfo($"Заблокирован неактивный пир {peer.EndPoint}");
                    }
                }
            }

            // Управление отдачей: разблокируем пиров, которые заинтересованы в наших данных
            // Для отдачи используем ту же логику, но проверяем IsChoked (AmChoking)
            var peersForUpload = _peers
                .Where(p => p.IsConnected && p.PeerInterested)
                .ToList();

            foreach (var peer in peersForUpload)
            {
                // Разблокируем пиров для отдачи, если они заинтересованы
                // Используем более агрессивную стратегию для отдачи - разблокируем больше пиров
                if (peer.IsChoked && peer.PeerInterested)
                {
                    await peer.SendUnchokeAsync();
                    Logger.LogInfo($"Разблокирован пир {peer.EndPoint} для отдачи");
                }
            }
        }

        #endregion
    }
}