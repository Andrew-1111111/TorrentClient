using TorrentClient.UI.Interfaces;

namespace TorrentClient.UI
{
    /// <summary>
    /// Управление троттлингом обновлений UI (SRP)
    /// </summary>
    public class UpdateThrottler : IUpdateThrottler
    {
        private const int MinUpdateIntervalMs = 1000;
        private const int MaxEntries = 1000;
        private readonly Dictionary<string, DateTime> _lastUpdateTime = [];

        public bool CanUpdate(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId))
                return false;

            var now = DateTime.UtcNow;
            if (_lastUpdateTime.TryGetValue(torrentId, out var lastUpdate))
            {
                if ((now - lastUpdate).TotalMilliseconds < MinUpdateIntervalMs)
                    return false;
            }
            
            return true;
        }

        public void MarkUpdated(string torrentId)
        {
            if (!string.IsNullOrEmpty(torrentId))
            {
                if (_lastUpdateTime.Count >= MaxEntries && !_lastUpdateTime.ContainsKey(torrentId))
                {
                    // Удаляем самые старые записи
                    var oldestEntries = _lastUpdateTime
                        .OrderBy(kv => kv.Value)
                        .Take(_lastUpdateTime.Count - MaxEntries + 100)
                        .Select(kv => kv.Key)
                        .ToList();
                    
                    foreach (var key in oldestEntries)
                    {
                        _lastUpdateTime.Remove(key);
                    }
                }
                
                _lastUpdateTime[torrentId] = DateTime.UtcNow;
            }
        }

        public void CleanupStaleEntries(IEnumerable<string> existingTorrentIds)
        {
            var existingSet = existingTorrentIds.ToHashSet();
            
            // Удаляем записи для несуществующих торрентов
            var keysToRemove = _lastUpdateTime.Keys
                .Where(id => !existingSet.Contains(id))
                .ToList();
            
            foreach (var key in keysToRemove)
            {
                _lastUpdateTime.Remove(key);
            }
            
            // Также очищаем записи старше 1 часа
            var cutoffTime = DateTime.UtcNow.AddHours(-1);
            var staleKeys = _lastUpdateTime
                .Where(kv => kv.Value < cutoffTime)
                .Select(kv => kv.Key)
                .ToList();
            
            foreach (var key in staleKeys)
            {
                _lastUpdateTime.Remove(key);
            }
        }
    }
}

