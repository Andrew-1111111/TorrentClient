using TorrentClient.UI.Interfaces;

namespace TorrentClient.UI
{
    /// <summary>
    /// Обновление списка торрентов в UI (SRP)
    /// </summary>
    public class TorrentListViewUpdater(IUpdateThrottler throttler) : ITorrentListViewUpdater
    {
        private const int MinUpdateIntervalMs = 1000;
        private readonly Dictionary<string, DateTime> _lastUpdateTime = [];
        private readonly IUpdateThrottler _throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));

        public void UpdateTorrentsList(ListView listView, List<Torrent> torrents, 
            out long totalDownloadSpeed, out long totalUploadSpeed)
        {
            totalDownloadSpeed = 0;
            totalUploadSpeed = 0;
            
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

        public void UpdateTorrentItem(ListViewItem item, Torrent torrent)
        {
            if (item == null || torrent == null || item.SubItems.Count < 9) return;

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
            {
                // Приоритет: 0 = Низкий, 1 = Нормальный, 2 = Высокий
                var priorityText = torrent.Priority switch
                {
                    0 => "Низкий",
                    1 => "Нормальный",
                    2 => "Высокий",
                    _ => "Нормальный"
                };
                item.SubItems[7].Text = priorityText; // Приоритет
            }
            if (item.SubItems.Count > 8)
                item.SubItems[8].Text = torrent.State.ToString(); // Статус

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
            
            newTooltip += $"\nПиры: {torrent.ActivePeers} активных / {torrent.ConnectedPeers} подключённых / {torrent.TotalPeers} всего";
            
            if (item.ToolTipText != newTooltip)
                item.ToolTipText = newTooltip;
            
            // Добавляем отдельные tooltips для колонок Скорость и Пиры
            UpdateColumnTooltips(item, torrent);
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
            // Конвертация согласно стандарту: https://en.wikipedia.org/wiki/Data-rate_units
            // 1 Mbps = 1,000,000 bits/s = 125,000 bytes/s
            // 1 MB/s = 1,000,000 bytes/s = 8,000,000 bits/s
            double mbps = (bytesPerSecond * 8.0) / 1_000_000.0;
            double mbytes = bytesPerSecond / 1_000_000.0;
            
            if (mbps >= 1000)
            {
                double gbps = mbps / 1000.0;
                double gbytes = bytesPerSecond / 1_000_000_000.0;
                return $"{gbps:0.##} Gbps ({gbytes:0.##} GB/s)";
            }
            else if (mbps >= 1)
            {
                return $"{mbps:0.##} Mbps ({mbytes:0.##} MB/s)";
            }
            else if (mbps >= 0.001)
            {
                double kbps = mbps * 1000.0;
                double kbytes = bytesPerSecond / 1_000.0;
                return $"{kbps:0.#} Kbps ({kbytes:0.#} KB/s)";
            }
            else
            {
                return "0 Mbps (0 MB/s)";
            }
        }

        /// <summary>
        /// Форматирует лимит скорости для отображения (Mbps или MB/s)
        /// </summary>
        /// <remarks>
        /// Согласно стандарту: https://en.wikipedia.org/wiki/Data-rate_units
        /// 1 Mbps = 1,000,000 bits/s = 125,000 bytes/s
        /// 1 MB/s = 1,000,000 bytes/s = 8,000,000 bits/s
        /// </remarks>
        private static string FormatSpeedLimit(long bytesPerSecond)
        {
            // Конвертируем bytes/sec в Mbps: bytes * 8 / 1,000,000
            double mbps = bytesPerSecond * 8.0 / 1_000_000.0;
            if (mbps >= 1.0)
                return $"{mbps:F1} Mbps";
            
            // Для малых значений показываем в MB/s (байты/сек / 1,000,000)
            double mbytes = bytesPerSecond / 1_000_000.0;
            return $"{mbytes:F2} MB/s";
        }
        
        /// <summary>
        /// Обновляет tooltips для отдельных колонок (Скорость и Пиры)
        /// </summary>
        private static void UpdateColumnTooltips(ListViewItem item, Models.Torrent torrent)
        {
            if (item.ListView == null)
                return;
                
            // Tooltip для колонки Скорость (индекс 4)
            if (item.SubItems.Count > 4)
            {
                var speedTooltip = $"Текущая скорость загрузки: {FormatSpeed(torrent.DownloadSpeed)}\n" +
                    $"Текущая скорость отдачи: {FormatSpeed(torrent.UploadSpeed)}\n\n";
                
                if (torrent.MaxDownloadSpeed.HasValue || torrent.MaxUploadSpeed.HasValue)
                {
                    speedTooltip += "Ограничения скорости:\n";
                    if (torrent.MaxDownloadSpeed.HasValue)
                        speedTooltip += $"  Загрузка: {FormatSpeedLimit(torrent.MaxDownloadSpeed.Value)}\n";
                    else
                        speedTooltip += $"  Загрузка: без ограничений\n";
                    if (torrent.MaxUploadSpeed.HasValue)
                        speedTooltip += $"  Отдача: {FormatSpeedLimit(torrent.MaxUploadSpeed.Value)}\n";
                    else
                        speedTooltip += $"  Отдача: без ограничений\n";
                }
                else
                {
                    speedTooltip += "Ограничения скорости: не установлены";
                }
                
                // Сохраняем tooltip в Tag подэлемента (ListView не поддерживает tooltips для подэлементов напрямую)
                // Используем специальный формат в Tag для идентификации
                var currentSpeedTag = item.SubItems[4].Tag?.ToString();
                if (currentSpeedTag != speedTooltip)
                    item.SubItems[4].Tag = speedTooltip;
            }
            
            // Tooltip для колонки Пиры (индекс 6)
            if (item.SubItems.Count > 6)
            {
                var peersTooltip = $"Активные пиры: {torrent.ActivePeers}\n" +
                    $"  Пиры, которые разблокированы и могут отправлять данные\n\n" +
                    $"Подключённые пиры: {torrent.ConnectedPeers}\n" +
                    $"  Пиры, с которыми установлено соединение\n\n" +
                    $"Всего пиров: {torrent.TotalPeers}\n" +
                    $"  Общее количество известных пиров (включая неактивных)";
                
                var currentPeersTag = item.SubItems[6].Tag?.ToString();
                if (currentPeersTag != peersTooltip)
                    item.SubItems[6].Tag = peersTooltip;
            }
        }
    }
}

