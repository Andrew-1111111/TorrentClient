namespace TorrentClient.Protocol.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий DhtClient (замена событий)
    /// </summary>
    public interface IDhtCallbacks
    {
        /// <summary>Вызывается при обнаружении пиров</summary>
        Task OnPeersFoundAsync(List<IPEndPoint> peers);
    }
}

