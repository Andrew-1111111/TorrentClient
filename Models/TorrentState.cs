namespace TorrentClient.Models
{
    /// <summary>
    /// Состояние торрента
    /// </summary>
    public enum TorrentState
    {
        /// <summary>Остановлен</summary>
        Stopped,
        
        /// <summary>Проверка/инициализация</summary>
        Checking,
        
        /// <summary>На паузе</summary>
        Paused,
        
        /// <summary>Загружается</summary>
        Downloading,
        
        /// <summary>Раздается (сидится)</summary>
        Seeding,
        
        /// <summary>Ошибка</summary>
        Error
    }
}

