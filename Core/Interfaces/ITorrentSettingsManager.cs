namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления настройками торрентов
    /// Разделение интерфейсов по принципу ISP
    /// </summary>
    public interface ITorrentSettingsManager
    {
        /// <summary>Устанавливает лимит скорости для торрента</summary>
        void SetTorrentSpeedLimit(string torrentId, long? maxDownloadSpeed, long? maxUploadSpeed);
        
        /// <summary>Устанавливает приоритет файла в торренте</summary>
        void SetFilePriority(string torrentId, string filePath, int priority);
        
        /// <summary>Применяет глобальные настройки</summary>
        void ApplyGlobalSettings(int maxConnections, int maxHalfOpenConnections, 
            int maxPiecesToRequest, int maxRequestsPerPeer);
        
        /// <summary>Обновляет глобальные настройки из AppSettings</summary>
        void UpdateGlobalSettings(AppSettings settings);
    }
}

