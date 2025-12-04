using System;
using System.Collections.Generic;
using System.Linq;

namespace TorrentClient.Models
{
    /// <summary>
    /// Метаданные торрента, извлеченные из .torrent файла
    /// </summary>
    public class TorrentInfo
    {
        /// <summary>Название торрента</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Общий размер всех файлов в торренте в байтах</summary>
        public long TotalSize { get; set; }
        
        /// <summary>SHA-1 хэш словаря "info" в формате hex строки</summary>
        public string InfoHash { get; set; } = string.Empty;
        
        /// <summary>Размер одного куска в байтах</summary>
        public int PieceLength { get; set; }
        
        /// <summary>Количество кусков (вместо хранения всех хэшей для экономии памяти)</summary>
        public int PieceCount { get; set; }
        
        /// <summary>Список файлов в торренте</summary>
        public List<TorrentFile> Files { get; set; } = new();
        
        /// <summary>Список URL трекеров</summary>
        public List<string> AnnounceUrls { get; set; } = new();
        
        /// <summary>Комментарий к торренту</summary>
        public string? Comment { get; set; }
        
        /// <summary>Название программы, создавшей торрент</summary>
        public string? CreatedBy { get; set; }
        
        /// <summary>Дата создания торрента</summary>
        public DateTime? CreationDate { get; set; }
        
        /// <summary>Кодировка строк в торренте</summary>
        public string? Encoding { get; set; }
    }

    /// <summary>
    /// Информация об одном файле в торренте
    /// </summary>
    public class TorrentFile
    {
        /// <summary>Путь к файлу относительно корня торрента</summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>Размер файла в байтах</summary>
        public long Length { get; set; }
        
        /// <summary>Смещение файла в общем потоке данных торрента в байтах</summary>
        public long Offset { get; set; }
    }
}

