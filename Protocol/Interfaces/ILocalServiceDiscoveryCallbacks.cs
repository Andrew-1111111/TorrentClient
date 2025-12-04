namespace TorrentClient.Protocol.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий LocalServiceDiscovery (замена событий)
    /// </summary>
    public interface ILocalServiceDiscoveryCallbacks
    {
        /// <summary>Вызывается при обнаружении пира</summary>
        Task OnPeerDiscoveredAsync(IPEndPoint peer);
    }
}

