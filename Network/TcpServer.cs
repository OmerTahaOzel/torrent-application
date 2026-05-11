using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Torrent.Core;
using Torrent.Models;

namespace Torrent.Network
{
    public class TcpServer
    {
        private TcpListener? _listener;
        private bool _isRunning;
        private readonly TorrentEngine _engine;

        public TcpServer(TorrentEngine engine)
        {
            _engine = engine;
        }

        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[BİLGİ] P2P Sunucusu {port} portunda başlatıldı.");

            Task.Run(AcceptClientsAsync);
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    if (_listener == null)
                        return;

                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    string remoteIp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    _engine.AddPeer(remoteIp, port: 0);

                    _ = Task.Run(() => HandleClientAsync(client, remoteIp));
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        Console.WriteLine($"[HATA] Dinleme hatası: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, string remoteIp)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    while (_isRunning && client.Connected)
                    {
                        P2PMessage? message = await NetworkMessageIO.ReadMessageAsync(stream);
                        if (message == null)
                            break;

                        message.SenderIp = remoteIp;
                        _engine.ProcessMessage(message, remoteIp);

                        if (message.Type == MessageType.Handshake || message.Type == MessageType.Metadata)
                        {
                            await SendSyncMessagesAsync(stream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] {remoteIp} bağlantı hatası: {ex.Message}");
            }
        }

        private async Task SendSyncMessagesAsync(NetworkStream stream)
        {
            await NetworkMessageIO.WriteMessageAsync(stream, _engine.CreateBitfieldMessage());

            FileMetadata? metadata = _engine.ActiveMetadata;
            if (metadata == null)
                return;

            foreach (int pieceIndex in _engine.GetMissingPieceIndexes())
            {
                P2PMessage request = new P2PMessage
                {
                    Type = MessageType.RequestPiece,
                    PieceIndex = pieceIndex,
                    Payload = Array.Empty<byte>()
                };

                await NetworkMessageIO.WriteMessageAsync(stream, request);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}
