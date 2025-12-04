namespace TorrentClient.UI.Services
{
    /// <summary>
    /// Утилита для проверки дискового пространства
    /// </summary>
    public static class DiskSpaceChecker
    {
        /// <summary>Минимальный резерв места на диске (1 GB)</summary>
        private const long MinReserveBytes = 1L * 1024 * 1024 * 1024;

        /// <summary>
        /// Результат проверки дискового пространства
        /// </summary>
        public class CheckResult
        {
            public bool HasEnoughSpace { get; set; }
            public string? WarningMessage { get; set; }
            public string? DriveRoot { get; set; }
            public long RequiredBytes { get; set; }
            public long AvailableBytes { get; set; }
            public long MissingBytes { get; set; }
        }

        /// <summary>
        /// Проверяет наличие достаточного места на диске
        /// </summary>
        /// <param name="path">Путь для проверки</param>
        /// <param name="requiredBytes">Требуемое количество байт</param>
        /// <returns>Результат проверки</returns>
        public static CheckResult CheckDiskSpace(string path, long requiredBytes)
        {
            var result = new CheckResult
            {
                RequiredBytes = requiredBytes,
                HasEnoughSpace = false
            };

            try
            {
                var root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root))
                {
                    result.WarningMessage = "Невозможно определить диск для выбранного пути.";
                    return result;
                }

                result.DriveRoot = root;
                var driveInfo = new DriveInfo(root);
                result.AvailableBytes = driveInfo.AvailableFreeSpace;
                var neededSpace = requiredBytes + MinReserveBytes;

                if (result.AvailableBytes < neededSpace)
                {
                    result.MissingBytes = neededSpace - result.AvailableBytes;
                    result.WarningMessage = FormatDiskSpaceMessage(
                        root, 
                        requiredBytes, 
                        result.AvailableBytes, 
                        result.MissingBytes);
                    return result;
                }

                result.HasEnoughSpace = true;
                return result;
            }
            catch (Exception ex)
            {
                result.WarningMessage = $"Ошибка проверки дискового пространства: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Форматирует сообщение о недостатке места на диске
        /// </summary>
        private static string FormatDiskSpaceMessage(string driveRoot, long requiredBytes, long availableBytes, long missingBytes)
        {
            return $"Недостаточно места на диске {driveRoot}\n\n" +
                   $"Требуется: {FormatBytes(requiredBytes)}\n" +
                   $"Доступно: {FormatBytes(availableBytes)}\n" +
                   $"Не хватает: {FormatBytes(missingBytes)}";
        }

        /// <summary>
        /// Форматирует байты в читаемый формат
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            var sizes = new[] { "B", "KB", "MB", "GB", "TB" };
            var len = (double)bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

