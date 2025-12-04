using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TorrentClient.Protocol.Interfaces;
using TorrentClient.Utilities;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Local Service Discovery (LSD) - поиск локальных пиров в сети
    /// Реализация BEP14: http://www.bittorrent.org/beps/bep_0014.html
    /// </summary>
    public class LocalServiceDiscovery : IDisposable
    {
        private UdpClient? _udpClient;
        private readonly string _infoHash;
        private readonly int _port;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _listenerTask;
        private const int LsdPort = 6771; // Стандартный порт для LSD
        private const string LsdMulticastAddress = "239.192.152.143"; // Стандартный multicast адрес для LSD
        private DateTime _lastAnnounce = DateTime.MinValue;
        private const int AnnounceInterval = 5 * 60; // Объявляем каждые 5 минут

        #region Асинхронные колбэки (замена событий)

        private ILocalServiceDiscoveryCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(ILocalServiceDiscoveryCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        public LocalServiceDiscovery(string infoHash, int port)
        {
            _infoHash = infoHash;
            _port = port;
        }

        public void Start()
        {
            try
            {
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, LsdPort));
                
                // Присоединяемся к multicast группе
                var multicastAddress = IPAddress.Parse(LsdMulticastAddress);
                _udpClient.JoinMulticastGroup(multicastAddress);
                
                Logger.LogInfo($"[LSD] Started listening on {LsdMulticastAddress}:{LsdPort} for info hash: {_infoHash}");

                _listenerTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
                
                // Отправляем первое объявление
                AnnounceAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError("[LSD] Error starting LSD", ex);
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            if (_udpClient == null) return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        HandleLsdMessage(result.Buffer, result.RemoteEndPoint);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[LSD] Error receiving message", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[LSD] Error in listener loop", ex);
            }
        }

        private void HandleLsdMessage(byte[] data, IPEndPoint remoteEndPoint)
        {
            try
            {
                // LSD сообщение: "BT-SEARCH * HTTP/1.1\r\n" + headers
                var message = Encoding.ASCII.GetString(data);
                
                if (!message.StartsWith("BT-SEARCH"))
                    return;

                var lines = message.Split(new[] { "\r\n" }, StringSplitOptions.None);
                Dictionary<string, string> headers = [];
                
                foreach (var line in lines)
                {
                    if (line.Contains(':'))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            headers[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                // Проверяем, что это для нашего торрента
                if (headers.TryGetValue("Infohash", out var infoHash) && 
                    infoHash.Equals(_infoHash, StringComparison.OrdinalIgnoreCase))
                {
                    // Извлекаем порт пира
                    if (headers.TryGetValue("Port", out var portStr) && 
                        int.TryParse(portStr, out var port))
                    {
                        var peer = new IPEndPoint(remoteEndPoint.Address, port);
                        Logger.LogInfo($"[LSD] Discovered local peer: {peer} for info hash: {_infoHash}");
                        if (_callbacks != null)
                        {
                            var peerCopy = peer; // Захватываем копию для лямбды
                            // КРИТИЧНО: Используем SafeTaskRunner для предотвращения утечки памяти через необработанные исключения
                            SafeTaskRunner.RunSafe(async () => await _callbacks.OnPeerDiscoveredAsync(peerCopy).ConfigureAwait(false));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[LSD] Error handling LSD message from {remoteEndPoint}", ex);
            }
        }

        public async Task AnnounceAsync()
        {
            // Ограничиваем частоту объявлений
            if ((DateTime.Now - _lastAnnounce).TotalSeconds < AnnounceInterval)
                return;

            try
            {
                if (_udpClient == null)
                    return;

                // Формируем LSD announce сообщение
                var message = $"BT-SEARCH * HTTP/1.1\r\n" +
                             $"Host: {LsdMulticastAddress}:{LsdPort}\r\n" +
                             $"Port: {_port}\r\n" +
                             $"Infohash: {_infoHash}\r\n" +
                             $"cookie: {Guid.NewGuid()}\r\n" +
                             $"\r\n";

                var data = Encoding.ASCII.GetBytes(message);
                var multicastEndPoint = new IPEndPoint(IPAddress.Parse(LsdMulticastAddress), LsdPort);
                
                await _udpClient.SendAsync(data, data.Length, multicastEndPoint);
                _lastAnnounce = DateTime.Now;
                
                Logger.LogInfo($"[LSD] Sent announce for info hash: {_infoHash} on port: {_port}");
            }
            catch (Exception ex)
            {
                Logger.LogError("[LSD] Error sending announce", ex);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
            // КРИТИЧНО: Обнуляем задачу для предотвращения утечки памяти
            _listenerTask = null;
            
            // Очищаем колбэки для предотвращения утечек памяти
            _callbacks = null;
            
            try
            {
                if (_udpClient != null)
                {
                    var multicastAddress = IPAddress.Parse(LsdMulticastAddress);
                    _udpClient.DropMulticastGroup(multicastAddress);
                    _udpClient.Close();
                    _udpClient.Dispose();
                }
            }
            catch { }
            
            _cancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

