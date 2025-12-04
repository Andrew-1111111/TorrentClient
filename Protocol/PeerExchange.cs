using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TorrentClient.Protocol.Interfaces;
using TorrentClient.Utilities;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Peer Exchange (PEX) - обмен списками пиров между подключенными пирами
    /// Реализация BEP11: http://www.bittorrent.org/beps/bep_0011.html
    /// </summary>
    public class PeerExchange
    {
        private readonly PeerConnection _connection;
        private bool _pexSupported = false;
        private byte _pexMessageId = 0;
        private readonly List<IPEndPoint> _knownPeers = new();
        private DateTime _lastPexSend = DateTime.MinValue;
        private const int PexSendInterval = 30; // Отправляем PEX каждые 30 секунд (увеличена частота для более активного обмена)

        #region Асинхронные колбэки (замена событий)

        private IPeerExchangeCallbacks? _callbacks;

        /// <summary>Устанавливает колбэки для замены событий</summary>
        public void SetCallbacks(IPeerExchangeCallbacks callbacks)
        {
            _callbacks = callbacks;
        }

        #endregion

        public PeerExchange(PeerConnection connection)
        {
            _connection = connection;
        }

        /// <summary>
        /// Проверяет, поддерживает ли пир PEX через extension handshake
        /// </summary>
        public bool IsSupported => _pexSupported;

        /// <summary>
        /// Отправляет extension handshake с поддержкой PEX
        /// </summary>
        public async Task SendExtensionHandshakeAsync()
        {
            try
            {
                // Extension handshake: bencoded dictionary с поддерживаемыми расширениями
                // "m" - dictionary с mapping extension name -> extension id
                // "p" - наш порт (опционально)
                // "v" - версия клиента (опционально)
                var extensions = new Dictionary<string, object>
                {
                    { "m", new Dictionary<string, object>
                        {
                            { "ut_pex", 1 } // PEX extension с ID 1
                        }
                    }
                };

                var bencoded = BencodeEncode(extensions);
                await SendExtensionMessageAsync(0, bencoded); // ID расширения 0 = handshake
                Logger.LogInfo($"[PEX] Sent extension handshake to {_connection.EndPoint}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PEX] Error sending extension handshake to {_connection.EndPoint}", ex);
            }
        }

        /// <summary>
        /// Обрабатывает extension handshake и определяет поддержку PEX
        /// </summary>
        public void HandleExtensionHandshake(Dictionary<string, object> extensions)
        {
            // Extension handshake содержит dictionary с ключом "m" (mapping)
            if (extensions.TryGetValue("m", out var mObj) && mObj is Dictionary<string, object> mapping)
            {
                if (mapping.ContainsKey("ut_pex"))
                {
                    _pexSupported = true;
                    var pexValue = mapping["ut_pex"];
                    if (pexValue is long pexLong)
                    {
                        _pexMessageId = (byte)pexLong;
                    }
                    else if (pexValue is int pexInt)
                    {
                        _pexMessageId = (byte)pexInt;
                    }
                    else if (pexValue is byte[] pexBytes && pexBytes.Length > 0)
                    {
                        _pexMessageId = pexBytes[0];
                    }
                    else if (pexValue is byte pexByte)
                    {
                        _pexMessageId = pexByte;
                    }
                    else
                    {
                        _pexMessageId = 1; // По умолчанию
                    }
                    Logger.LogInfo($"[PEX] Peer {_connection.EndPoint} supports PEX (message ID: {_pexMessageId})");
                }
            }
        }

        /// <summary>
        /// Обрабатывает PEX сообщение от пира
        /// </summary>
        public void HandlePexMessage(byte[] data)
        {
            try
            {
                // PEX сообщение: bencoded dictionary с ключами "added" и "added.f"
                // "added" - compact format список пиров (6 байт на пира: 4 байта IP + 2 байта порт)
                // "added.f" - флаги для каждого пира (1 байт на пира)
                
                var peers = ParsePexPeers(data);
                // ОПТИМИЗАЦИЯ: Используем более эффективную проверку
                if (peers.Count != 0)
                {
                    Logger.LogInfo($"[PEX] Received {peers.Count} peers from {_connection.EndPoint}");
                    if (_callbacks != null)
                    {
                        var peersCopy = peers; // Захватываем копию для лямбды
                        // КРИТИЧНО: Используем SafeTaskRunner для предотвращения утечки памяти через необработанные исключения
                        SafeTaskRunner.RunSafe(async () => await _callbacks.OnPeersReceivedAsync(peersCopy).ConfigureAwait(false));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PEX] Error parsing PEX message from {_connection.EndPoint}", ex);
            }
        }

        /// <summary>
        /// Отправляет список пиров другому пиру через PEX
        /// </summary>
        public async Task SendPexPeersAsync(List<IPEndPoint> peers)
        {
            if (!_pexSupported || _pexMessageId == 0)
                return;

            // ОПТИМИЗАЦИЯ: Используем UtcNow для лучшей производительности
            // Ограничиваем частоту отправки PEX
            if ((DateTime.UtcNow - _lastPexSend).TotalSeconds < PexSendInterval)
                return;

            try
            {
                // ОПТИМИЗАЦИЯ: Предварительно вычисляем размер для избежания переаллокаций
                int maxPeers = Math.Min(50, peers.Count);
                var compactPeers = new byte[maxPeers * 6]; // 6 байт на пира (4 IP + 2 порт)
                int offset = 0;
                
                foreach (var peer in peers)
                {
                    if (offset >= compactPeers.Length)
                        break;
                        
                    var ipBytes = peer.Address.GetAddressBytes();
                    if (ipBytes.Length == 4) // IPv4
                    {
                        // ОПТИМИЗАЦИЯ: Прямое копирование IP без промежуточных аллокаций
                        Array.Copy(ipBytes, 0, compactPeers, offset, 4);
                        offset += 4;
                        
                        // ОПТИМИЗАЦИЯ: Прямое преобразование порта в big-endian без Array.Reverse
                        var port = (ushort)peer.Port;
                        compactPeers[offset++] = (byte)(port >> 8);
                        compactPeers[offset++] = (byte)(port & 0xFF);
                    }
                }

                if (offset == 0)
                    return;

                // ОПТИМИЗАЦИЯ: Обрезаем массив до реального размера
                if (offset < compactPeers.Length)
                {
                    var trimmed = new byte[offset];
                    Array.Copy(compactPeers, 0, trimmed, 0, offset);
                    compactPeers = trimmed;
                }

                // Формируем bencoded dictionary
                var dict = new Dictionary<string, object>
                {
                    { "added", compactPeers }
                };

                // Кодируем в bencode
                var bencoded = BencodeEncode(dict);
                
                // Отправляем extension message
                await SendExtensionMessageAsync(_pexMessageId, bencoded);
                
                _lastPexSend = DateTime.UtcNow;
                Logger.LogInfo($"[PEX] Sent {peers.Count} peers to {_connection.EndPoint}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PEX] Error sending PEX peers to {_connection.EndPoint}", ex);
            }
        }

        private static List<IPEndPoint> ParsePexPeers(byte[] data)
        {
            List<IPEndPoint> peers = [];
            
            try
            {
                // Парсим bencoded dictionary
                var dict = BencodeDecode(data) as Dictionary<string, object>;
                if (dict == null)
                    return peers;

                if (dict.TryGetValue("added", out var addedObj) && addedObj is byte[] addedBytes)
                {
                    // ОПТИМИЗАЦИЯ: Предварительно вычисляем количество пиров для избежания переаллокаций
                    int peerCount = addedBytes.Length / 6;
                    if (peerCount > 0)
                    {
                        peers.Capacity = peerCount; // ОПТИМИЗАЦИЯ: Предустановка capacity
                        
                        // Compact format: 6 байт на пира (4 байта IP + 2 байта порт)
                        for (int i = 0; i <= addedBytes.Length - 6; i += 6)
                        {
                        // ОПТИМИЗАЦИЯ: Прямое создание IPAddress из массива
                        // Используем конструктор с массивом и offset для избежания копирования
                        var ipBytes = new byte[4];
                        Array.Copy(addedBytes, i, ipBytes, 0, 4);
                        var ip = new IPAddress(ipBytes);

                            // ОПТИМИЗАЦИЯ: Прямое чтение порта в big-endian без Array.Reverse
                            var port = (ushort)((addedBytes[i + 4] << 8) | addedBytes[i + 5]);

                            peers.Add(new IPEndPoint(ip, port));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PEX] Error parsing PEX peers", ex);
            }

            return peers;
        }

        private async Task SendExtensionMessageAsync(byte messageId, byte[] data)
        {
            // Расширенное сообщение: <len=0001+X><id=20><id_расширения><данные>
            var message = new byte[2 + data.Length];
            message[0] = 20; // ID расширенного сообщения
            message[1] = messageId; // ID расширения (ut_pex)
            Array.Copy(data, 0, message, 2, data.Length);

            // ОПТИМИЗАЦИЯ: Используем internal метод напрямую вместо рефлексии
            await _connection.SendMessageAsync(message);
        }

        // ОПТИМИЗИРОВАННАЯ реализация bencode encoding для PEX
        private static byte[] BencodeEncode(Dictionary<string, object> dict)
        {
            // ОПТИМИЗАЦИЯ: Предварительно вычисляем размер для избежания переаллокаций
            int estimatedSize = 2; // 'd' + 'e'
            foreach (var kvp in dict)
            {
                estimatedSize += kvp.Key.Length + 20; // ключ + длина ключа
                if (kvp.Value is byte[] bytes)
                {
                    estimatedSize += bytes.Length + 20; // данные + длина данных
                }
            }
            
            var result = new List<byte>(estimatedSize);
            result.Add((byte)'d'); // начало словаря

            foreach (var kvp in dict)
            {
                // Ключ
                var keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                // ОПТИМИЗАЦИЯ: Используем более эффективное преобразование числа в строку
                var keyLengthStr = keyBytes.Length.ToString();
                result.AddRange(Encoding.UTF8.GetBytes(keyLengthStr));
                result.Add((byte)':');
                result.AddRange(keyBytes);

                // Значение
                if (kvp.Value is byte[] bytes)
                {
                    // ОПТИМИЗАЦИЯ: Используем более эффективное преобразование числа в строку
                    var valueLengthStr = bytes.Length.ToString();
                    result.AddRange(Encoding.UTF8.GetBytes(valueLengthStr));
                    result.Add((byte)':');
                    result.AddRange(bytes);
                }
            }

            result.Add((byte)'e'); // конец словаря
            return result.ToArray();
        }

        // Простая реализация bencode decoding для PEX
        private static object? BencodeDecode(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            int pos = 0;
            return DecodeValue(data, ref pos);
        }

        private static object? DecodeValue(byte[] data, ref int pos)
        {
            if (pos >= data.Length)
                return null;

            if (data[pos] == 'd')
            {
                // Словарь
                pos++;
                Dictionary<string, object> dict = [];
                while (pos < data.Length && data[pos] != 'e')
                {
                    var key = DecodeString(data, ref pos);
                    if (key == null) break;
                    var value = DecodeValue(data, ref pos);
                    if (value != null)
                        dict[key] = value;
                }
                if (pos < data.Length && data[pos] == 'e')
                    pos++;
                return dict;
            }
            else if (data[pos] >= '0' && data[pos] <= '9')
            {
                // Строка
                return DecodeString(data, ref pos);
            }

            return null;
        }

        private static string? DecodeString(byte[] data, ref int pos)
        {
            // ОПТИМИЗАЦИЯ: Прямое вычисление длины без StringBuilder
            int lengthStart = pos;
            while (pos < data.Length && data[pos] >= '0' && data[pos] <= '9')
            {
                pos++;
            }
            
            if (pos >= data.Length || data[pos] != ':')
                return null;
            
            // ОПТИМИЗАЦИЯ: Прямое преобразование ASCII цифр в число без строки
            int length = 0;
            for (int i = lengthStart; i < pos; i++)
            {
                length = length * 10 + (data[i] - '0');
            }
            
            pos++; // пропускаем ':'
            
            if (pos + length > data.Length)
                return null;

            var str = Encoding.UTF8.GetString(data, pos, length);
            pos += length;
            return str;
        }
    }
}

