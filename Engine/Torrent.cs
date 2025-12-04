using System.Security.Cryptography;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Метаданные торрента, распарсенные из .torrent файла
    /// Основано на BEP 3: http://www.bittorrent.org/beps/bep_0003.html
    /// </summary>
    public class TorrentMetadata
    {
        #region Свойства

        /// <summary>Название торрента</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>InfoHash (20 байт)</summary>
        public byte[] InfoHash { get; set; } = new byte[20];
        
        /// <summary>InfoHash в HEX формате</summary>
        public string InfoHashHex => Convert.ToHexString(InfoHash);
        
        /// <summary>Общий размер (байт)</summary>
        public long TotalLength { get; set; }
        
        /// <summary>Размер куска (байт)</summary>
        public int PieceLength { get; set; }
        
        /// <summary>Количество кусков</summary>
        public int PieceCount => PieceHashCount > 0 ? PieceHashCount : (int)Math.Ceiling((double)TotalLength / PieceLength);
        
        /// <summary>Все хэши кусков в одном массиве (20 байт на хэш)</summary>
        public byte[] PieceHashesData { get; set; } = Array.Empty<byte>();
        
        /// <summary>Количество хэшей кусков</summary>
        public int PieceHashCount { get; set; }
        
        /// <summary>Список файлов</summary>
        public List<TorrentFileInfo> Files { get; set; } = new();
        
        /// <summary>Список трекеров</summary>
        public List<string> Trackers { get; set; } = new();
        
        /// <summary>Комментарий</summary>
        public string? Comment { get; set; }
        
        /// <summary>Создатель</summary>
        public string? CreatedBy { get; set; }
        
        /// <summary>Дата создания</summary>
        public DateTime? CreationDate { get; set; }
        
        /// <summary>Приватный торрент</summary>
        public bool IsPrivate { get; set; }

        #endregion

        #region Методы

        /// <summary>Получает размер указанного куска</summary>
        public int GetPieceLength(int pieceIndex)
        {
            if (pieceIndex < 0 || pieceIndex >= PieceCount)
                throw new ArgumentOutOfRangeException(nameof(pieceIndex));

            if (pieceIndex == PieceCount - 1)
            {
                // Последний кусок может быть меньше
                var remainder = TotalLength % PieceLength;
                return remainder > 0 ? (int)remainder : PieceLength;
            }
            return PieceLength;
        }

        /// <summary>Проверяет кусок по хэшу</summary>
        public bool VerifyPiece(int pieceIndex, byte[] data)
        {
            if (pieceIndex < 0 || pieceIndex >= PieceHashCount)
                return false;

            var hash = SHA1.HashData(data);
            var expectedHash = PieceHashesData.AsSpan(pieceIndex * 20, 20);
            return hash.AsSpan().SequenceEqual(expectedHash);
        }
        
        /// <summary>Получает хэш куска по индексу</summary>
        public ReadOnlySpan<byte> GetPieceHash(int pieceIndex)
        {
            if (pieceIndex < 0 || pieceIndex >= PieceHashCount)
                throw new ArgumentOutOfRangeException(nameof(pieceIndex));
            
            return PieceHashesData.AsSpan(pieceIndex * 20, 20);
        }

        /// <summary>Получает файлы, пересекающиеся с куском</summary>
        public IEnumerable<(TorrentFileInfo File, long FileOffset, int PieceOffset, int Length)> GetFilesForPiece(int pieceIndex)
        {
            long pieceStart = (long)pieceIndex * PieceLength;
            long pieceEnd = pieceStart + GetPieceLength(pieceIndex);

            foreach (var file in Files)
            {
                long fileEnd = file.Offset + file.Length;

                if (pieceStart < fileEnd && pieceEnd > file.Offset)
                {
                    long overlapStart = Math.Max(pieceStart, file.Offset);
                    long overlapEnd = Math.Min(pieceEnd, fileEnd);
                    int length = (int)(overlapEnd - overlapStart);

                    yield return (
                        file,
                        overlapStart - file.Offset,
                        (int)(overlapStart - pieceStart),
                        length
                    );
                }
            }
        }

        #endregion
    }

    /// <summary>Информация о файле в торренте</summary>
    public class TorrentFileInfo
    {
        /// <summary>Путь к файлу</summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>Размер файла</summary>
        public long Length { get; set; }
        
        /// <summary>Смещение в торренте</summary>
        public long Offset { get; set; }
    }

    /// <summary>
    /// Парсер .torrent файлов
    /// </summary>
    public static class TorrentParser
    {
        private static readonly BencodeParser _parser = new();

        /// <summary>Парсит торрент из файла</summary>
        public static TorrentMetadata Parse(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            return Parse(bytes);
        }

        /// <summary>Асинхронно парсит торрент из файла</summary>
        public static async Task<TorrentMetadata> ParseAsync(string filePath)
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            return Parse(bytes);
        }

        /// <summary>Парсит торрент из байтов</summary>
        public static TorrentMetadata Parse(byte[] data)
        {
            var dict = _parser.Parse<BDictionary>(data);
            return ParseDictionary(dict, data);
        }

        private static TorrentMetadata ParseDictionary(BDictionary dict, byte[] originalData)
        {
            var metadata = new TorrentMetadata();

            // Парсим трекеры
            if (dict.TryGetValue("announce", out var announce) && announce is BString announceStr)
            {
                metadata.Trackers.Add(announceStr.ToString());
            }

            if (dict.TryGetValue("announce-list", out var announceList) && announceList is BList list)
            {
                foreach (var tier in list)
                {
                    if (tier is BList tierList)
                    {
                        foreach (var tracker in tierList)
                        {
                            if (tracker is BString trackerStr)
                            {
                                var url = trackerStr.ToString();
                                if (!string.IsNullOrEmpty(url) && !metadata.Trackers.Contains(url))
                                {
                                    metadata.Trackers.Add(url);
                                }
                            }
                        }
                    }
                }
            }

            // Парсим комментарий
            if (dict.TryGetValue("comment", out var comment) && comment is BString commentStr)
            {
                metadata.Comment = commentStr.ToString();
            }

            // Парсим создателя
            if (dict.TryGetValue("created by", out var createdBy) && createdBy is BString createdByStr)
            {
                metadata.CreatedBy = createdByStr.ToString();
            }

            // Парсим дату создания
            if (dict.TryGetValue("creation date", out var creationDate) && creationDate is BNumber dateNum)
            {
                metadata.CreationDate = DateTimeOffset.FromUnixTimeSeconds(dateNum.Value).DateTime;
            }

            // Парсим info словарь
            if (!dict.TryGetValue("info", out var infoObj) || infoObj is not BDictionary info)
            {
                throw new InvalidDataException("Отсутствует или неверный словарь 'info'");
            }

            // Вычисляем InfoHash
            metadata.InfoHash = CalculateInfoHash(originalData);

            // Парсим название
            if (info.TryGetValue("name", out var name) && name is BString nameStr)
            {
                metadata.Name = nameStr.ToString();
            }

            // Парсим размер куска
            if (info.TryGetValue("piece length", out var pieceLength) && pieceLength is BNumber pieceLengthNum)
            {
                metadata.PieceLength = (int)pieceLengthNum.Value;
            }

            // Парсим флаг приватности
            if (info.TryGetValue("private", out var privateFlag) && privateFlag is BNumber privateNum)
            {
                metadata.IsPrivate = privateNum.Value == 1;
            }

            // Парсим хэши кусков (SHA1) - храним в одном массиве для экономии памяти
            if (info.TryGetValue("pieces", out var pieces) && pieces is BString piecesStr)
            {
                var piecesBytes = piecesStr.Value.ToArray();
                // Обрезаем до кратного 20 (размер SHA1 хэша)
                int validLength = (piecesBytes.Length / 20) * 20;
                metadata.PieceHashesData = new byte[validLength];
                Array.Copy(piecesBytes, metadata.PieceHashesData, validLength);
                metadata.PieceHashCount = validLength / 20;
            }

            // Парсим файлы
            if (info.TryGetValue("files", out var files) && files is BList filesList)
            {
                // Многофайловый торрент
                long offset = 0;
                foreach (var fileObj in filesList)
                {
                    if (fileObj is BDictionary fileDict)
                    {
                        var fileInfo = new TorrentFileInfo { Offset = offset };

                        if (fileDict.TryGetValue("length", out var length) && length is BNumber lengthNum)
                        {
                            fileInfo.Length = lengthNum.Value;
                        }

                        if (fileDict.TryGetValue("path", out var path) && path is BList pathList)
                        {
                            var pathParts = pathList
                                .OfType<BString>()
                                .Select(s => s.ToString())
                                .ToList();
                            fileInfo.Path = System.IO.Path.Combine(metadata.Name, System.IO.Path.Combine(pathParts.ToArray()));
                        }

                        metadata.Files.Add(fileInfo);
                        offset += fileInfo.Length;
                    }
                }
                metadata.TotalLength = offset;
            }
            else if (info.TryGetValue("length", out var singleLength) && singleLength is BNumber singleLengthNum)
            {
                // Однофайловый торрент
                metadata.TotalLength = singleLengthNum.Value;
                metadata.Files.Add(new TorrentFileInfo
                {
                    Path = metadata.Name,
                    Length = metadata.TotalLength,
                    Offset = 0
                });
            }

            Logger.LogInfo($"[TorrentParser] Распарсен торрент: {metadata.Name}");
            Logger.LogInfo($"[TorrentParser]   InfoHash: {metadata.InfoHashHex}");
            Logger.LogInfo($"[TorrentParser]   Размер: {metadata.TotalLength:N0} байт");
            Logger.LogInfo($"[TorrentParser]   Кусков: {metadata.PieceCount} x {metadata.PieceLength:N0} байт");
            Logger.LogInfo($"[TorrentParser]   Файлов: {metadata.Files.Count}");
            Logger.LogInfo($"[TorrentParser]   Трекеров: {metadata.Trackers.Count}");

            return metadata;
        }

        /// <summary>Вычисляет InfoHash из сырых данных торрента</summary>
        private static byte[] CalculateInfoHash(byte[] torrentData)
        {
            // Находим info словарь в сырых байтах и хэшируем его
            // Необходимо использовать сырые байты для точного хэша

            // Ищем "4:info" в данных
            var infoKey = Encoding.ASCII.GetBytes("4:info");
            int infoStart = -1;

            for (int i = 0; i < torrentData.Length - infoKey.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < infoKey.Length; j++)
                {
                    if (torrentData[i + j] != infoKey[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                {
                    infoStart = i + infoKey.Length;
                    break;
                }
            }

            if (infoStart == -1)
            {
                throw new InvalidDataException("Не удалось найти словарь info");
            }

            // Находим конец info словаря
            int depth = 0;
            int infoEnd = infoStart;
            bool inString = false;
            int stringLength = 0;
            int stringLengthDigits = 0;

            for (int i = infoStart; i < torrentData.Length; i++)
            {
                byte b = torrentData[i];

                if (inString)
                {
                    stringLength--;
                    if (stringLength == 0)
                    {
                        inString = false;
                    }
                    continue;
                }

                if (b >= '0' && b <= '9')
                {
                    // Начало длины строки
                    stringLengthDigits = stringLengthDigits * 10 + (b - '0');
                    continue;
                }

                if (b == ':' && stringLengthDigits > 0)
                {
                    stringLength = stringLengthDigits;
                    stringLengthDigits = 0;
                    inString = true;
                    continue;
                }

                stringLengthDigits = 0;

                if (b == 'd' || b == 'l')
                {
                    depth++;
                }
                else if (b == 'e')
                {
                    depth--;
                    if (depth == 0)
                    {
                        infoEnd = i + 1;
                        break;
                    }
                }
                else if (b == 'i')
                {
                    // Целое число - ищем 'e'
                    while (i < torrentData.Length && torrentData[i] != 'e')
                    {
                        i++;
                    }
                }
            }

            // Хэшируем info словарь
            return SHA1.HashData(torrentData.AsSpan(infoStart, infoEnd - infoStart));
        }
    }
}
