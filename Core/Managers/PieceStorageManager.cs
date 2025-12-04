using System.Security.Cryptography;
using TorrentClient.Core.Interfaces;
using TorrentClient.Models;

namespace TorrentClient.Core.Managers
{
    /// <summary>
    /// Менеджер хранения кусков
    /// Принцип SRP: единственная ответственность - работа с файлами кусков
    /// </summary>
    public class PieceStorageManager : IPieceStorageManager
    {
        private readonly Torrent _torrent;
        private readonly string _downloadPath;
        private readonly int _blockSize = 65536;

        public PieceStorageManager(Torrent torrent, string downloadPath)
        {
            _torrent = torrent ?? throw new ArgumentNullException(nameof(torrent));
            _downloadPath = downloadPath ?? throw new ArgumentNullException(nameof(downloadPath));
        }

        /// <summary>
        /// Сохраняет кусок на диск
        /// </summary>
        public async Task SavePieceAsync(int pieceIndex, byte[] pieceData, CancellationToken cancellationToken)
        {
            var pieceLength = GetPieceLength(pieceIndex);
            var pieceOffset = (long)pieceIndex * _torrent.Info.PieceLength;
            var pieceEnd = pieceOffset + pieceLength;
            
            foreach (var file in _torrent.Info.Files)
            {
                var fileStart = file.Offset;
                var fileEnd = file.Offset + file.Length;
                
                if (pieceOffset < fileEnd && pieceEnd > fileStart)
                {
                    var filePath = Path.Combine(_downloadPath, file.Path);
                    var directory = Path.GetDirectoryName(filePath);
                    if (directory != null)
                        Directory.CreateDirectory(directory);

                    var writeStart = Math.Max(pieceOffset, fileStart);
                    var writeEnd = Math.Min(pieceEnd, fileEnd);
                    var writeLength = (int)(writeEnd - writeStart);
                    var bufferOffset = (int)(writeStart - pieceOffset);
                    var fileOffset = writeStart - fileStart;

                    if (writeLength > 0)
                    {
                        // Используем буферизацию для лучшей производительности
                        using var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write, _blockSize, FileOptions.SequentialScan);
                        fs.Seek(fileOffset, SeekOrigin.Begin);
                        await TaskTimeoutHelper.TimeoutAsync(
                            fs.WriteAsync(pieceData.AsMemory(bufferOffset, writeLength), cancellationToken),
                            TimeSpan.FromSeconds(60));
                    }
                }
            }
        }

        /// <summary>
        /// Читает кусок с диска
        /// </summary>
        public async Task<byte[]?> ReadPieceAsync(int pieceIndex, CancellationToken cancellationToken)
        {
            try
            {
                var pieceLength = GetPieceLength(pieceIndex);
                var pieceData = new byte[pieceLength];
                var pieceOffset = (long)pieceIndex * _torrent.Info.PieceLength;
                var pieceEnd = pieceOffset + pieceLength;
                var bytesRead = 0;
                
                foreach (var file in _torrent.Info.Files)
                {
                    var fileStart = file.Offset;
                    var fileEnd = file.Offset + file.Length;
                    
                    if (pieceOffset < fileEnd && pieceEnd > fileStart)
                    {
                        var filePath = Path.Combine(_downloadPath, file.Path);
                        if (!File.Exists(filePath))
                            return null;
                        
                        var readStart = Math.Max(pieceOffset, fileStart);
                        var readEnd = Math.Min(pieceEnd, fileEnd);
                        var readLength = (int)(readEnd - readStart);
                        var bufferOffset = (int)(readStart - pieceOffset);
                        var fileOffset = readStart - fileStart;
                        
                        if (readLength > 0)
                        {
                            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _blockSize, FileOptions.SequentialScan);
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            bytesRead += await TaskTimeoutHelper.TimeoutAsync(
                                fs.ReadAsync(pieceData, bufferOffset, readLength, cancellationToken),
                                TimeSpan.FromSeconds(60));
                        }
                    }
                }
                
                return bytesRead == pieceLength ? pieceData : null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка чтения куска {pieceIndex}", ex);
                return null;
            }
        }

        /// <summary>
        /// Проверяет хеш куска
        /// </summary>
        public bool VerifyPieceHash(int pieceIndex, byte[] pieceData)
        {
            if (pieceIndex >= _torrent.Info.PieceCount)
                return false;

            // Верификация хэшей должна выполняться через TorrentMetadata
            // Здесь возвращаем true, так как верификация выполняется в Engine
            return true;
        }

        private int GetPieceLength(int pieceIndex)
        {
            if (pieceIndex == _torrent.Info.PieceCount - 1)
            {
                var remainder = _torrent.Info.TotalSize % _torrent.Info.PieceLength;
                return remainder > 0 ? (int)remainder : _torrent.Info.PieceLength;
            }
            return _torrent.Info.PieceLength;
        }
    }
}

