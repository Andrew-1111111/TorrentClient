namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления состоянием торрентов (сохранение/загрузка)
    /// Разделение интерфейсов по принципу ISP
    /// </summary>
    public interface ITorrentStateManager
    {
        /// <summary>Загружает сохранённые торренты</summary>
        void LoadSavedTorrents();
        
        /// <summary>Сохраняет все торренты</summary>
        void SaveAllTorrents();
        
        /// <summary>Синхронизирует состояние всех торрентов</summary>
        void SyncAllTorrentsState();
    }
}

