using TorrentClient.Protocol;

namespace TorrentClient.Protocol.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий PeerConnection (замена событий)
    /// </summary>
    public interface IPeerConnectionCallbacks
    {
        /// <summary>Вызывается при получении Piece</summary>
        Task OnPieceReceivedAsync(PeerConnection.PieceDataEventArgs e);
        
        /// <summary>Вызывается при закрытии соединения</summary>
        Task OnConnectionClosedAsync();
        
        /// <summary>Вызывается при обновлении bitfield пира</summary>
        Task OnPeerBitfieldUpdatedAsync();
        
        /// <summary>Вызывается при получении Have сообщения</summary>
        Task OnHaveReceivedAsync(PeerConnection.HaveEventArgs e);
        
        /// <summary>Вызывается при получении Request</summary>
        Task OnRequestReceivedAsync(PeerConnection.RequestEventArgs e);
        
        /// <summary>Вызывается при получении пиров через PEX</summary>
        Task OnPexPeersReceivedAsync(List<IPEndPoint> peers);
    }
}

