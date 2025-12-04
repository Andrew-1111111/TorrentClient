using System.Collections.Generic;
using System.Windows.Forms;
using TorrentClient.UI.Interfaces;

namespace TorrentClient.UI
{
    /// <summary>
    /// Обновление списка торрентов в UI (SRP)
    /// </summary>
    public class TorrentListViewUpdater : ITorrentListViewUpdater
    {
        private const int MinUpdateIntervalMs = 1000;
        private readonly Dictionary<string, DateTime> _lastUpdateTime = [];
        private readonly IUpdateThrottler _throttler;

        public TorrentListViewUpdater(IUpdateThrottler throttler)
        {
            _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
        }

        public void UpdateTorrentsList(ListView listView, List<Models.Torrent> torrents, 
            out long totalDownloadSpeed, out long totalUploadSpeed)
        {
            totalDownloadSpeed = 0;
            totalUploadSpeed = 0;
            
            var now = DateTime.UtcNow;
            foreach (var torrent in torrents)
            {
                // Проверяем троттлинг
                if (!_throttler.CanUpdate(torrent.Id))
                {
                    totalDownloadSpeed += torrent.DownloadSpeed;
                    totalUploadSpeed += torrent.UploadSpeed;
                    continue;
                }

                // Ищем элемент в ListView
                ListViewItem? item = null;
                foreach (ListViewItem listItem in listView.Items)
                {
                    if (listItem.Tag?.ToString() == torrent.Id)
                    {
                        item = listItem;
                        break;
                    }
                }

                if (item != null)
                {
                    UpdateTorrentItem(item, torrent);
                    _throttler.MarkUpdated(torrent.Id);
                }
                
                // Суммируем скорость всех торрентов
                totalDownloadSpeed += torrent.DownloadSpeed;
                totalUploadSpeed += torrent.UploadSpeed;
            }
        }

        public void UpdateTorrentItem(ListViewItem item, Models.Torrent torrent)
        {
            if (item == null || torrent == null || item.SubItems.Count < 8) return;

            // Обновляем номер (если изменился порядок)
            if (item.ListView != null)
            {
                var number = item.ListView.Items.IndexOf(item) + 1;
                if (item.Text != number.ToString())
                    item.Text = number.ToString();
            }

            // Обновляем подэлементы
            if (item.SubItems.Count > 1)
            {
                // Добавляем иконку ограничения скорости в название, если установлены лимиты
                var nameText = torrent.Info.Name;
                if (torrent.MaxDownloadSpeed.HasValue || torrent.MaxUploadSpeed.HasValue)
                {
                    nameText = "⚡ " + nameText;
                }
                item.SubItems[1].Text = nameText; // Название
            }
            if (item.SubItems.Count > 2)
                item.SubItems[2].Text = FormatBytes(torrent.Info.TotalSize); // Размер
            if (item.SubItems.Count > 3)
            {
                var progress = torrent.Info.TotalSize > 0 
                    ? (double)torrent.DownloadedBytes / torrent.Info.TotalSize * 100 
                    : 0;
                item.SubItems[3].Text = $"{progress:F1}%"; // Прогресс
            }
            if (item.SubItems.Count > 4)
            {
                // Показываем текущую скорость и лимиты, если они установлены
                var speedText = FormatSpeed(torrent.DownloadSpeed);
                if (torrent.MaxDownloadSpeed.HasValue || torrent.MaxUploadSpeed.HasValue)
                {
                    var limits = new List<string>();
                    if (torrent.MaxDownloadSpeed.HasValue)
                        limits.Add($"↓{FormatSpeedLimit(torrent.MaxDownloadSpeed.Value)}");
                    if (torrent.MaxUploadSpeed.HasValue)
                        limits.Add($"↑{FormatSpeedLimit(torrent.MaxUploadSpeed.Value)}");
                    speedText += $" [{string.Join(", ", limits)}]";
                }
                item.SubItems[4].Text = speedText; // Скорость
            }
            if (item.SubItems.Count > 5)
                item.SubItems[5].Text = FormatBytes(torrent.DownloadedBytes); // Загружено
            if (item.SubItems.Count > 6)
                item.SubItems[6].Text = $"{torrent.ActivePeers}/{torrent.ConnectedPeers}/{torrent.TotalPeers}"; // Пиры
            if (item.SubItems.Count > 7)
                item.SubItems[7].Text = torrent.State.ToString(); // Статус

            // Обновляем подсказку с информацией о лимитах
            var newTooltip = $"{torrent.Info.Name}\n\n" +
                $"Размер: {FormatBytes(torrent.Info.TotalSize)}\n" +
                $"Загружено: {FormatBytes(torrent.DownloadedBytes)} / {FormatBytes(torrent.Info.TotalSize)}\n" +
                $"Прогресс: {(torrent.Info.TotalSize > 0 ? (double)torrent.DownloadedBytes / torrent.Info.TotalSize * 100 : 0):F1}%\n" +
                $"Скорость загрузки: {FormatSpeed(torrent.DownloadSpeed)}\n" +
                $"Скорость отдачи: {FormatSpeed(torrent.UploadSpeed)}\n";
            
            // Добавляем информацию о лимитах в tooltip
            if (torrent.MaxDownloadSpeed.HasValue || torrent.MaxUploadSpeed.HasValue)
            {
                newTooltip += "\nОграничения скорости:\n";
                if (torrent.MaxDownloadSpeed.HasValue)
                    newTooltip += $"  Загрузка: {FormatSpeedLimit(torrent.MaxDownloadSpeed.Value)}\n";
                else
                    newTooltip += $"  Загрузка: без ограничений\n";
                if (torrent.MaxUploadSpeed.HasValue)
                    newTooltip += $"  Отдача: {FormatSpeedLimit(torrent.MaxUploadSpeed.Value)}\n";
                else
                    newTooltip += $"  Отдача: без ограничений\n";
            }
            
            newTooltip += $"Пиры: {torrent.ActivePeers} активных / {torrent.ConnectedPeers} подключённых / {torrent.TotalPeers} всего";
            
            if (item.ToolTipText != newTooltip)
                item.ToolTipText = newTooltip;
        }

        public void CleanupStaleUpdateTimes(ListView listView)
        {
            HashSet<string> existingTorrentIds = [];
            foreach (ListViewItem item in listView.Items)
            {
                var id = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    existingTorrentIds.Add(id);
                }
            }
            
            _throttler.CleanupStaleEntries(existingTorrentIds);
        }

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

        private static string FormatSpeed(long bytesPerSecond)
        {
            double mbps = (bytesPerSecond * 8.0) / 1_000_000.0;
            double mbytes = bytesPerSecond / 1_048_576.0;
            
            if (mbps >= 1000)
                return $"{mbps / 1000:0.##} Gbps ({mbytes / 1024:0.##} GB/s)";
            else if (mbps >= 1)
                return $"{mbps:0.##} Mbps ({mbytes:0.##} MB/s)";
            else
                return $"{bytesPerSecond / 1024.0:0.##} KB/s";
        }

        /// <summary>
        /// Форматирует лимит скорости для отображения (только Mbps)
        /// </summary>
        private static string FormatSpeedLimit(long bytesPerSecond)
        {
            // Конвертируем bytes/sec в Mbps: bytes * 8 / 1,000,000
            double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
            if (mbps >= 1.0)
                return $"{mbps:F1} Mbps";
            var kbps = mbps * 1000.0;
            return $"{kbps:F1} Kbps";
        }
    }
}

