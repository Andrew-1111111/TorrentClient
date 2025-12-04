using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TorrentClient.Utilities;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Менеджер запросов блоков - управление запросами к пирам
    /// </summary>
    public class RequestManager : IDisposable
    {
        #region Поля

        private readonly Dictionary<PeerConnection, Queue<BlockRequest>> _pendingRequests = [];
        private readonly Dictionary<string, BlockRequest> _activeRequests = [];
        private readonly SemaphoreSlim _lock = new(1, 1);

        #endregion

        #region Свойства

        /// <summary>
        /// Максимум одновременных запросов от одного пира (pipelining)
        /// Увеличенное значение критично для высокой скорости загрузки!
        /// </summary>
        public int MaxRequestsPerPeer { get; set; } = 200;

        #endregion

        #region Публичные методы

        /// <summary>
        /// Добавляет запрос блока в очередь для пира
        /// </summary>
        public async Task<bool> RequestBlockAsync(PeerConnection peer, int pieceIndex, int begin, int length, TaskCompletionSource<byte[]> tcs)
        {
            await _lock.WaitAsync();
            try
            {
                // Проверка лимита запросов для пира
                var activePeerRequests = _activeRequests.Values.Count(r => r.Peer == peer);
                if (activePeerRequests >= MaxRequestsPerPeer)
                {
                    return false;
                }

                var key = $"{pieceIndex}_{begin}";
                if (_activeRequests.ContainsKey(key))
                {
                    return false;
                }

                var request = new BlockRequest
                {
                    PieceIndex = pieceIndex,
                    Begin = begin,
                    Length = length,
                    Peer = peer,
                    RequestTime = DateTime.Now,
                    CompletionSource = tcs
                };

                if (!_pendingRequests.ContainsKey(peer))
                    _pendingRequests[peer] = [];

                _pendingRequests[peer].Enqueue(request);
                _activeRequests[key] = request;

                // Отправка запроса пиру
                await peer.SendRequestAsync(pieceIndex, begin, length);

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Обрабатывает полученный блок
        /// </summary>
        public async Task<bool> HandleBlockReceivedAsync(PeerConnection peer, int pieceIndex, int begin, byte[] data)
        {
            await _lock.WaitAsync();
            try
            {
                var key = $"{pieceIndex}_{begin}";
                if (!_activeRequests.TryGetValue(key, out var request))
                {
                    Logger.LogWarning($"Получен неожиданный блок: кусок={pieceIndex}, смещение={begin}");
                    return false;
                }

                if (request.Peer != peer)
                {
                    Logger.LogWarning($"Блок получен от неверного пира: ожидался {request.Peer.EndPoint}, получен {peer.EndPoint}");
                    return false;
                }

                _activeRequests.Remove(key);
                
                // Удаление из очереди пира
                if (_pendingRequests.TryGetValue(peer, out var queue))
                {
                    var items = queue.Where(r => r.PieceIndex != pieceIndex || r.Begin != begin).ToList();
                    queue.Clear();
                    foreach (var item in items)
                        queue.Enqueue(item);
                }

                // Завершение задачи
                request.CompletionSource?.TrySetResult(data);

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Отменяет все запросы для пира
        /// </summary>
        public async Task CancelPeerRequestsAsync(PeerConnection peer)
        {
            await _lock.WaitAsync();
            try
            {
                if (_pendingRequests.TryGetValue(peer, out var queue))
                {
                    while (queue.Count > 0)
                    {
                        var request = queue.Dequeue();
                        var key = $"{request.PieceIndex}_{request.Begin}";
                        _activeRequests.Remove(key);
                        request.CompletionSource?.TrySetCanceled();
                    }
                    _pendingRequests.Remove(peer);
                }

                // Отмена активных запросов
                var activeRequests = _activeRequests.Values.Where(r => r.Peer == peer).ToList();
                foreach (var request in activeRequests)
                {
                    var key = $"{request.PieceIndex}_{request.Begin}";
                    _activeRequests.Remove(key);
                    request.CompletionSource?.TrySetCanceled();
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Получает количество активных запросов для пира
        /// </summary>
        public int GetActiveRequestCount(PeerConnection peer)
        {
            _lock.Wait();
            try
            {
                return _activeRequests.Values.Count(r => r.Peer == peer);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Очищает запросы, которые превысили таймаут (по умолчанию 60 секунд)
        /// </summary>
        public async Task CleanupTimedOutRequestsAsync(TimeSpan timeout)
        {
            await _lock.WaitAsync();
            try
            {
                var now = DateTime.Now;
                var timedOutRequests = _activeRequests.Values
                    .Where(r => (now - r.RequestTime) > timeout)
                    .ToList();

                foreach (var request in timedOutRequests)
                {
                    var key = $"{request.PieceIndex}_{request.Begin}";
                    _activeRequests.Remove(key);
                    
                    // Удаление из очереди пира
                    if (_pendingRequests.TryGetValue(request.Peer, out var queue))
                    {
                        var items = queue.Where(r => r.PieceIndex != request.PieceIndex || r.Begin != request.Begin).ToList();
                        queue.Clear();
                        foreach (var item in items)
                            queue.Enqueue(item);
                    }
                    
                    // Отмена задачи с таймаутом
                    request.CompletionSource?.TrySetException(new TimeoutException($"Запрос блока превысил таймаут: кусок={request.PieceIndex}, смещение={request.Begin}"));
                    Logger.LogWarning($"Таймаут запроса: кусок={request.PieceIndex}, смещение={request.Begin}, пир={request.Peer.EndPoint}");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            // Очищаем словари для освобождения ссылок на PeerConnection
            _lock.Wait();
            try
            {
                foreach (var request in _activeRequests.Values)
                {
                    request.CompletionSource?.TrySetCanceled();
                }
                
                foreach (var queue in _pendingRequests.Values)
                {
                    while (queue.Count > 0)
                    {
                        var request = queue.Dequeue();
                        request.CompletionSource?.TrySetCanceled();
                    }
                }
                
                _pendingRequests.Clear();
                _activeRequests.Clear();
            }
            finally
            {
                _lock.Release();
            }
            
            _lock.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Вложенные классы

        /// <summary>
        /// Запрос блока
        /// </summary>
        public class BlockRequest
        {
            public int PieceIndex { get; set; }
            public int Begin { get; set; }
            public int Length { get; set; }
            public PeerConnection Peer { get; set; } = null!;
            public DateTime RequestTime { get; set; }
            public TaskCompletionSource<byte[]>? CompletionSource { get; set; }
        }

        #endregion
    }
}
