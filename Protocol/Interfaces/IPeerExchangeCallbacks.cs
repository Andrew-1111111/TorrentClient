namespace TorrentClient.Protocol.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий PeerExchange (замена событий)
    /// </summary>
    public interface IPeerExchangeCallbacks
    {
        /// <summary>Вызывается при получении пиров через PEX</summary>
        Task OnPeersReceivedAsync(List<IPEndPoint> peers);
    }
}

