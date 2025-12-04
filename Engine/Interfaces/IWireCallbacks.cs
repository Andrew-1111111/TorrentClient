using TorrentClient.Engine;

namespace TorrentClient.Engine.Interfaces
{
    /// <summary>
    /// Асинхронные колбэки для событий Wire (замена событий)
    /// </summary>
    public interface IWireCallbacks
    {
        /// <summary>Вызывается при подключении</summary>
        Task OnConnectedAsync();
        
        /// <summary>Вызывается при отключении</summary>
        Task OnDisconnectedAsync();
        
        /// <summary>Вызывается при получении handshake</summary>
        Task OnHandshakeReceivedAsync();
        
        /// <summary>Вызывается при получении Have сообщения</summary>
        Task OnHaveAsync(int pieceIndex);
        
        /// <summary>Вызывается при получении Bitfield</summary>
        Task OnBitfieldAsync(BitArray bitfield);
        
        /// <summary>Вызывается при получении Piece</summary>
        Task OnPieceAsync(Wire.PieceData pieceData);
        
        /// <summary>Вызывается при получении Request</summary>
        Task OnRequestAsync(Wire.BlockRequest request);
        
        /// <summary>Вызывается при получении Choke</summary>
        Task OnChokeAsync();
        
        /// <summary>Вызывается при получении Unchoke</summary>
        Task OnUnchokeAsync();
        
        /// <summary>Вызывается при получении Interested</summary>
        Task OnInterestedAsync();
        
        /// <summary>Вызывается при получении NotInterested</summary>
        Task OnNotInterestedAsync();
        
        /// <summary>Вызывается при получении Extended сообщения</summary>
        Task OnExtendedAsync(byte[] data);
        
        /// <summary>Вызывается при ошибке</summary>
        Task OnErrorAsync(Exception error);
    }
}

