using System.Collections.Generic;
using System.Linq;

namespace TorrentClient.Protocol
{
    public class TrackerClientOptions
    {
        public Dictionary<string, string> TrackerCookies { get; }
        public Dictionary<string, Dictionary<string, string>> TrackerHeaders { get; }

        public TrackerClientOptions(
            Dictionary<string, string>? trackerCookies = null,
            Dictionary<string, Dictionary<string, string>>? trackerHeaders = null)
        {
            TrackerCookies = trackerCookies != null
                ? new Dictionary<string, string>(trackerCookies, System.StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            TrackerHeaders = trackerHeaders != null
                ? new Dictionary<string, Dictionary<string, string>>(trackerHeaders, System.StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Dictionary<string, string>>(System.StringComparer.OrdinalIgnoreCase);

            // Нормализуем вложенные словари заголовков, чтобы они также не учитывали регистр
            foreach (var key in TrackerHeaders.Keys.ToList())
            {
                TrackerHeaders[key] = new Dictionary<string, string>(TrackerHeaders[key], System.StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}

