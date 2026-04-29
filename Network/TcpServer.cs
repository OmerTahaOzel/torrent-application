using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Torrent.Core;

namespace Torrent.Network
{
    public class TcpServer
    {
        private TcpListener _listener;
        private bool _isRunning;
        private readonly P2PMessageQueue _messageQueue;
        private readonly TorrentEngine _engine;

        // Sunucu kalkarken patronu alıyor ve işçiye devrediyor
        public TcpServer(TorrentEngine engine)
        {
            _engine = engine;
            _messageQueue = new P2PMessageQueue(_engine);
        }

        public void Start(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[BİLGİ] P2P Sunucusu {port} portunda başlatıldı.");

            Task.Run(() => AcceptClientsAsync());
            _messageQueue.StartProcessing();
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    string remoteIp = client.Client.RemoteEndPoint.ToString();

                    // Patronun defterine bu adamı kaydediyoruz
                    _engine.AddPeer(remoteIp, port: 0);

                    _ = Task.Run(() => HandleClientAsync(client, remoteIp));
                }
                catch (Exception ex)
                {
                    if (_isRunning) Console.WriteLine($"[HATA] Dinleme hatası: {ex.Message}");
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
                    byte[] buffer = new byte[1048576 + 1024];

                    // İŞTE EKSİK OLAN KISIM BUYDU AMK! 
                    // Bağlantı açık olduğu sürece fedai kapıda sürekli mal beklemesi lazım
                    while (client.Connected)
                    {
                        // stream.ReadAsync karşıdan mesaj gelene kadar burada pusuya yatar
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                        // Eğer 0 byte okuduysa karşı taraf (Client) dükkanı kapatıp gitmiştir
                        if (bytesRead == 0) break;

                        byte[] receivedData = new byte[bytesRead];
                        Array.Copy(buffer, receivedData, bytesRead);

                        Torrent.Models.P2PMessage receivedMessage = Torrent.Models.MessageSerializer.Deserialize(receivedData);
                        receivedMessage.SenderIp = remoteIp;

                        // Paketi kuyruğa fırlat
                        _messageQueue.EnqueueMessage(receivedMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] {remoteIp} ile bağlantı koptu: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            _messageQueue.Stop();
        }
    }
}