using System;
using System.Collections.Generic;
using System.IO;

namespace TorrentClient.Models
{
    /// <summary>
    /// Представляет торрент с его состоянием и метаданными
    /// </summary>
    public class Torrent
    {
        /// <summary>Уникальный идентификатор торрента</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>Информация о торренте (метаданные)</summary>
        public TorrentInfo Info { get; set; } = new();
        
        /// <summary>Текущее состояние торрента</summary>
        public TorrentState State { get; set; } = TorrentState.Stopped;
        
        /// <summary>Путь для загрузки файлов торрента</summary>
        public string DownloadPath { get; set; } = string.Empty;
        
        /// <summary>Путь к файлу .torrent</summary>
        public string TorrentFilePath { get; set; } = string.Empty;
        
        /// <summary>Количество загруженных байт</summary>
        public long DownloadedBytes { get; set; }
        
        /// <summary>Количество отданных байт</summary>
        public long UploadedBytes { get; set; }
        
        /// <summary>Прогресс загрузки в процентах (0-100)</summary>
        public double Progress => Info.TotalSize > 0 ? (double)DownloadedBytes / Info.TotalSize * 100 : 0;
        
        /// <summary>Текущая скорость загрузки в байтах в секунду</summary>
        public long DownloadSpeed { get; set; }
        
        /// <summary>Текущая скорость отдачи в байтах в секунду</summary>
        public long UploadSpeed { get; set; }
        
        /// <summary>Количество подключенных пиров</summary>
        public int ConnectedPeers { get; set; }
        
        /// <summary>Количество активных (разблокированных) пиров</summary>
        public int ActivePeers { get; set; }
        
        /// <summary>Общее количество известных пиров</summary>
        public int TotalPeers { get; set; }
        
        /// <summary>Максимальная скорость загрузки в байтах в секунду (null = без ограничений)</summary>
        public long? MaxDownloadSpeed { get; set; }
        
        /// <summary>Максимальная скорость отдачи в байтах в секунду (null = без ограничений)</summary>
        public long? MaxUploadSpeed { get; set; }
        
        /// <summary>Битовое поле, показывающее какие куски уже загружены</summary>
        public BitField? BitField { get; set; }
        
        /// <summary>Список файлов в торренте</summary>
        public List<FileDownloadInfo> FileInfos { get; set; } = new();
        
        /// <summary>Дата добавления торрента</summary>
        public DateTime AddedDate { get; set; } = DateTime.Now;
        
        /// <summary>Дата завершения загрузки (null если не завершена)</summary>
        public DateTime? CompletedDate { get; set; }
        
        /// <summary>Показывает, завершена ли загрузка торрента</summary>
        public bool IsComplete => DownloadedBytes >= Info.TotalSize;
        
        /// <summary>Приоритет торрента (0 = низкий, 1 = нормальный, 2 = высокий)</summary>
        public int Priority { get; set; } = 1;
    }

    /// <summary>
    /// Информация о файле в торренте для загрузки
    /// </summary>
    public class FileDownloadInfo
    {
        /// <summary>Путь к файлу относительно папки загрузки</summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>Размер файла в байтах</summary>
        public long Length { get; set; }
        
        /// <summary>Количество загруженных байт файла</summary>
        public long Downloaded { get; set; }
        
        /// <summary>Максимальная скорость загрузки файла в байтах в секунду (null = без ограничений)</summary>
        public long? MaxSpeed { get; set; }
        
        /// <summary>Выбран ли файл для загрузки</summary>
        public bool IsSelected { get; set; } = true;
        
        /// <summary>Приоритет файла (0 = низкий, 1 = нормальный, 2 = высокий)</summary>
        public int Priority { get; set; } = 1;
    }
    
    /// <summary>
    /// Уровни приоритета файлов
    /// </summary>
    public enum FilePriority
    {
        /// <summary>Низкий приоритет - загружается последним</summary>
        Low = 0,
        /// <summary>Нормальный приоритет - загружается в обычном порядке</summary>
        Normal = 1,
        /// <summary>Высокий приоритет - загружается первым</summary>
        High = 2
    }
    
    /// <summary>
    /// Уровни приоритета торрентов
    /// </summary>
    public enum TorrentPriority
    {
        /// <summary>Низкий приоритет - загружается последним</summary>
        Low = 0,
        /// <summary>Нормальный приоритет - загружается в обычном порядке</summary>
        Normal = 1,
        /// <summary>Высокий приоритет - загружается первым</summary>
        High = 2
    }

    /// <summary>
    /// Битовое поле для отслеживания загруженных кусков торрента
    /// </summary>
    public class BitField
    {
        private readonly bool[] _bits;
        private int _setCount;

        /// <summary>
        /// Инициализирует новое битовое поле с указанным количеством кусков
        /// </summary>
        /// <param name="pieceCount">Количество кусков в торренте</param>
        public BitField(int pieceCount)
        {
            _bits = new bool[pieceCount];
            _setCount = 0;
        }

        /// <summary>
        /// Получает или устанавливает значение бита для указанного индекса куска
        /// </summary>
        /// <param name="index">Индекс куска</param>
        /// <returns>true если кусок загружен, false если нет</returns>
        public bool this[int index]
        {
            get => _bits[index];
            set
            {
                if (_bits[index] != value)
                {
                    _bits[index] = value;
                    if (value)
                        _setCount++;
                    else
                        _setCount--;
                }
            }
        }

        /// <summary>Общее количество кусков</summary>
        public int Length => _bits.Length;
        
        /// <summary>Количество загруженных кусков</summary>
        public int SetCount => _setCount;
        
        /// <summary>Показывает, все ли куски загружены</summary>
        public bool IsComplete => _setCount == _bits.Length;

        /// <summary>
        /// Преобразует битовое поле в массив байт (формат BitTorrent)
        /// </summary>
        /// <returns>Массив байт, представляющий битовое поле</returns>
        public byte[] ToByteArray()
        {
            int byteCount = (_bits.Length + 7) / 8;
            byte[] bytes = new byte[byteCount];
            for (int i = 0; i < _bits.Length; i++)
            {
                if (_bits[i])
                {
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    bytes[byteIndex] |= (byte)(1 << (7 - bitIndex));
                }
            }
            return bytes;
        }

        /// <summary>
        /// Загружает битовое поле из массива байт (формат BitTorrent)
        /// </summary>
        /// <param name="bytes">Массив байт, представляющий битовое поле</param>
        public void FromByteArray(byte[] bytes)
        {
            for (int i = 0; i < _bits.Length && i < bytes.Length * 8; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = i % 8;
                this[i] = (bytes[byteIndex] & (1 << (7 - bitIndex))) != 0;
            }
        }
    }
}

