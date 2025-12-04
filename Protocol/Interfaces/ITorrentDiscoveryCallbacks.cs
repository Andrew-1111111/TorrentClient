namespace TorrentClient.Protocol.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий TorrentDiscovery (замена событий)
    /// </summary>
    public interface ITorrentDiscoveryCallbacks
    {
        /// <summary>Вызывается при обнаружении пира</summary>
        Task OnPeerDiscoveredAsync(IPEndPoint peer);
    }
}

