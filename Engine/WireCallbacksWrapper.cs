using TorrentClient.Engine.Interfaces;

namespace TorrentClient.Engine
{
    /// <summary>
    /// Обёртка колбэков для Wire, которая перенаправляет вызовы в Swarm
    /// </summary>
    internal class WireCallbacksWrapper : IWireCallbacks
    {
        private readonly Swarm _swarm;
        private readonly Wire _wire;

        public WireCallbacksWrapper(Swarm swarm, Wire wire)
        {
            _swarm = swarm;
            _wire = wire;
        }

        public Task OnConnectedAsync()
        {
            // Подключение обрабатывается в Swarm через ConnectAsync
            return Task.CompletedTask;
        }

        public Task OnDisconnectedAsync()
        {
            _swarm.OnPeerDisconnected(_wire);
            return Task.CompletedTask;
        }

        public Task OnHandshakeReceivedAsync()
        {
            // Handshake обрабатывается в Swarm через ConnectAsync
            return Task.CompletedTask;
        }

        public Task OnHaveAsync(int pieceIndex)
        {
            _swarm.OnPeerHave(_wire, pieceIndex);
            return Task.CompletedTask;
        }

        public Task OnBitfieldAsync(BitArray bitfield)
        {
            _swarm.OnPeerBitfieldAsync(_wire, bitfield);
            return Task.CompletedTask;
        }

        public Task OnPieceAsync(Wire.PieceData pieceData)
        {
            _swarm.OnPieceReceivedAsync(_wire, pieceData);
            return Task.CompletedTask;
        }

        public Task OnRequestAsync(Wire.BlockRequest request)
        {
            _swarm.OnPeerRequestAsync(_wire, request);
            return Task.CompletedTask;
        }

        public Task OnChokeAsync()
        {
            // Choke обрабатывается в Swarm через состояние Wire
            return Task.CompletedTask;
        }

        public Task OnUnchokeAsync()
        {
            Swarm.OnPeerUnchoke(_wire);
            return Task.CompletedTask;
        }

        public Task OnInterestedAsync()
        {
            Swarm.OnPeerInterestedAsync(_wire);
            return Task.CompletedTask;
        }

        public Task OnNotInterestedAsync()
        {
            // NotInterested обрабатывается в Swarm через состояние Wire
            return Task.CompletedTask;
        }

        public Task OnExtendedAsync(byte[] data)
        {
            // Extended сообщения обрабатываются в Swarm при необходимости
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception error)
        {
            // Ошибки логируются в Wire, колбэк не требуется
            return Task.CompletedTask;
        }
    }
}

