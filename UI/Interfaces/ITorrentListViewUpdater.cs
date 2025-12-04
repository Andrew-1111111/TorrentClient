using System.Windows.Forms;

namespace TorrentClient.UI.Interfaces
{
    /// <summary>
    /// Интерфейс для обновления списка торрентов в UI
    /// </summary>
    public interface ITorrentListViewUpdater
    {
        /// <summary>Обновляет список торрентов в ListView</summary>
        void UpdateTorrentsList(ListView listView, List<Models.Torrent> torrents, 
            out long totalDownloadSpeed, out long totalUploadSpeed);
        
        /// <summary>Обновляет элемент торрента в ListView</summary>
        void UpdateTorrentItem(ListViewItem item, Models.Torrent torrent);
        
        /// <summary>Очищает устаревшие записи времени обновления</summary>
        void CleanupStaleUpdateTimes(ListView listView);
    }
}

