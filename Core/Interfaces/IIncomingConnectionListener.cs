using System.Net;

namespace TorrentClient.Core.Interfaces
{
    /// <summary>
    /// Интерфейс для управления входящими соединениями
    /// Принцип ISP: специфичный интерфейс только для входящих соединений
    /// </summary>
    public interface IIncomingConnectionListener
    {
        /// <summary>
        /// Запускает прослушивание входящих соединений
        /// </summary>
        void Start(CancellationToken cancellationToken);

        /// <summary>
        /// Останавливает прослушивание
        /// </summary>
        void Stop();

        /// <summary>
        /// Получает порт, на котором слушается входящие соединения
        /// </summary>
        int Port { get; }
    }
}

