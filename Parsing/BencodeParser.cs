using System.Security.Cryptography;

namespace TorrentClient.Parsing
{
    /// <summary>
    /// Парсер торрент файлов
    /// </summary>
    public class TorrentFileParser
    {
        private static readonly BencodeParser _parser = new();

        /// <summary>
        /// Парсит торрент файл и возвращает информацию о торренте
        /// </summary>
        /// <param name="filePath">Путь к файлу .torrent</param>
        /// <returns>Информация о торренте</returns>
        /// <exception cref="FileNotFoundException">Если файл не найден</exception>
        /// <exception cref="InvalidDataException">Если файл имеет неверный формат</exception>
        public static TorrentInfo ParseTorrentFile(string filePath)
        {
            var rawBytes = File.ReadAllBytes(filePath);
            var torrentDict = _parser.Parse<BDictionary>(rawBytes);
            return ConvertToTorrentInfo(torrentDict, rawBytes);
        }

        /// <summary>
        /// Парсит данные торрента из массива байт и возвращает информацию о торренте
        /// </summary>
        /// <param name="data">Массив байт с данными .torrent файла</param>
        /// <returns>Информация о торренте</returns>
        /// <exception cref="InvalidDataException">Если данные имеют неверный формат</exception>
        public static TorrentInfo ParseTorrentData(byte[] data)
        {
            var torrentDict = _parser.Parse<BDictionary>(data);
            return ConvertToTorrentInfo(torrentDict, data);
        }

        /// <summary>
        /// Преобразует Bencode словарь в TorrentInfo
        /// </summary>
        /// <param name="torrentDict">Bencode словарь с данными торрента</param>
        /// <param name="rawBytes">Исходные байты для вычисления InfoHash</param>
        /// <returns>Информация о торренте</returns>
        private static TorrentInfo ConvertToTorrentInfo(BDictionary torrentDict, byte[] rawBytes)
        {
            var info = new TorrentInfo();
            
            // Парсим info словарь
            if (!torrentDict.TryGetValue("info", out var infoObj) || infoObj is not BDictionary infoDict)
            {
                throw new InvalidDataException("Отсутствует словарь 'info'");
            }
            
            // Название
            if (infoDict.TryGetValue("name", out var nameObj) && nameObj is BString nameStr)
            {
                info.Name = nameStr.ToString();
            }
            
            // Комментарий
            if (torrentDict.TryGetValue("comment", out var commentObj) && commentObj is BString commentStr)
            {
                info.Comment = commentStr.ToString();
            }
            
            // Создатель
            if (torrentDict.TryGetValue("created by", out var createdByObj) && createdByObj is BString createdByStr)
            {
                info.CreatedBy = createdByStr.ToString();
            }
            
            // Дата создания
            if (torrentDict.TryGetValue("creation date", out var dateObj) && dateObj is BNumber dateNum)
            {
                info.CreationDate = DateTimeOffset.FromUnixTimeSeconds(dateNum.Value).DateTime;
            }
            
            // Кодировка
            if (torrentDict.TryGetValue("encoding", out var encodingObj) && encodingObj is BString encodingStr)
            {
                info.Encoding = encodingStr.ToString();
            }
            
            // Размер куска
            if (infoDict.TryGetValue("piece length", out var pieceLengthObj) && pieceLengthObj is BNumber pieceLengthNum)
            {
                info.PieceLength = (int)pieceLengthNum.Value;
            }

            // Добавляем announce URL
            if (torrentDict.TryGetValue("announce", out var announceValue) && announceValue is BString announceStr)
            {
                var announceUrl = announceStr.ToString();
                if (!string.IsNullOrEmpty(announceUrl) && !info.AnnounceUrls.Contains(announceUrl))
                {
                    info.AnnounceUrls.Add(announceUrl);
                }
            }
            
            // Добавляем announce-list
            if (torrentDict.TryGetValue("announce-list", out var announceListValue) && announceListValue is BList announceList)
            {
                foreach (var item in announceList)
                {
                    if (item is BList urlGroup)
                    {
                        foreach (var urlItem in urlGroup)
                        {
                            if (urlItem is BString urlStr)
                            {
                                var url = urlStr.ToString();
                                if (!string.IsNullOrEmpty(url) && !info.AnnounceUrls.Contains(url))
                                {
                                    info.AnnounceUrls.Add(url);
                                }
                            }
                        }
                    }
                    else if (item is BString singleUrl)
                    {
                        var url = singleUrl.ToString();
                        if (!string.IsNullOrEmpty(url) && !info.AnnounceUrls.Contains(url))
                        {
                            info.AnnounceUrls.Add(url);
                        }
                    }
                }
            }

            // Получаем InfoHash из сырых байтов
            info.InfoHash = CalculateInfoHashFromRawBytes(rawBytes);

            // Вычисляем количество кусков (не храним хэши для экономии памяти)
            if (infoDict.TryGetValue("pieces", out var piecesObj) && piecesObj is BString piecesStr)
            {
                var piecesBytes = piecesStr.Value.ToArray();
                const int pieceSize = 20; // SHA1 hash = 20 bytes
                info.PieceCount = piecesBytes.Length / pieceSize;
            }

            // Парсим файлы
            if (infoDict.TryGetValue("files", out var filesObj) && filesObj is BList filesList)
            {
                // Многофайловый торрент
                long offset = 0;
                foreach (var fileObj in filesList)
                {
                    if (fileObj is BDictionary fileDict)
                    {
                        long fileLength = 0;
                        if (fileDict.TryGetValue("length", out var lengthObj) && lengthObj is BNumber lengthNum)
                        {
                            fileLength = lengthNum.Value;
                        }

                        List<string> pathParts = [];
                        if (fileDict.TryGetValue("path", out var pathObj) && pathObj is BList pathList)
                        {
                            foreach (var pathPart in pathList)
                            {
                                if (pathPart is BString pathStr)
                                {
                                    pathParts.Add(pathStr.ToString());
                                }
                            }
                        }

                        // Безопасность: фильтруем опасные части пути
                        var safeParts = pathParts
                            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "." && p != ".." && 
                                       !p.Contains("..") && !Path.IsPathRooted(p))
                            .Select(p => p.Replace('\\', '/').Trim('/'))
                            .Where(p => !string.IsNullOrEmpty(p))
                            .ToList();
                        
                        var filePath = safeParts.Count > 0 ? Path.Combine([.. safeParts]) : string.Empty;
                        var fullPath = !string.IsNullOrEmpty(filePath) 
                            ? Path.Combine(info.Name, filePath)
                            : info.Name;

                        info.Files.Add(new TorrentFile
                        {
                            Path = fullPath,
                            Length = fileLength,
                            Offset = offset
                        });
                        offset += fileLength;
                    }
                }
                info.TotalSize = offset;
            }
            else if (infoDict.TryGetValue("length", out var lengthObj) && lengthObj is BNumber singleLengthNum)
            {
                // Однофайловый торрент
                info.TotalSize = singleLengthNum.Value;
                info.Files.Add(new TorrentFile
                {
                    Path = info.Name,
                    Length = info.TotalSize,
                    Offset = 0
                });
            }

            return info;
        }

        /// <summary>
        /// Вычисляет info_hash из сырых байтов торрент файла
        /// </summary>
        private static string CalculateInfoHashFromRawBytes(byte[] torrentData)
        {
            // Находим словарь info в сырых байтах и хэшируем его
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
                throw new InvalidDataException("Не удалось найти словарь 'info' в торрент файле");
            }

            // Находим конец словаря info, отслеживая глубину bencode
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

                if (b == ':')
                {
                    // Начало содержимого строки
                    stringLength = stringLengthDigits;
                    stringLengthDigits = 0;
                    if (stringLength > 0)
                    {
                        inString = true;
                    }
                    continue;
                }

                stringLengthDigits = 0;

                if (b == 'd' || b == 'l')
                {
                    depth++;
                }
                else if (b == 'i')
                {
                    // Целое число - пропускаем до 'e'
                    while (i < torrentData.Length && torrentData[i] != 'e')
                    {
                        i++;
                    }
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
            }

            // Извлекаем и хэшируем словарь info
            int infoLength = infoEnd - infoStart;
            var infoBytes = new byte[infoLength];
            Array.Copy(torrentData, infoStart, infoBytes, 0, infoLength);

            var hash = SHA1.HashData(infoBytes);
            return Convert.ToHexString(hash);
        }
    }
}
