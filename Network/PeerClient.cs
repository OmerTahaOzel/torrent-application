using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Torrent.Models;
using Torrent.IO; // FileManager'ı kullanmak için ekledik

namespace Torrent.Network
{
    public class PeerClient
    {
        private TcpClient _client;
        private NetworkStream _stream;

        public async Task ConnectToPeerAsync(string ipAddress, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ipAddress, port);
                _stream = _client.GetStream();

                Console.WriteLine($"[BAŞARILI] {ipAddress}:{port} ile bağlantı kuruldu.");

                // Bağlanır bağlanmaz racon gereği selamı çakıyoruz
                await SendHandshakeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] Bağlantı başarısız: {ex.Message}");
            }
        }

        private async Task SendHandshakeAsync()
        {
            P2PMessage handshakeMsg = new P2PMessage
            {
                Type = MessageType.Handshake,
                PieceIndex = -1,
                Payload = Array.Empty<byte>()
            };

            byte[] dataToSend = MessageSerializer.Serialize(handshakeMsg);
            await _stream.WriteAsync(dataToSend, 0, dataToSend.Length);
            Console.WriteLine("[BİLGİ] Handshake (Tokalaşma) gönderildi.");
        }

        // İŞTE YENİ SİLAHIMIZ: Diskten dosyayı okuyup karşıya fırlatan metot
        public async Task SendFilePieceAsync(string filePath, int pieceIndex)
        {
            try
            {
                // 1. Amele FileManager'ı çağır ve dosyayı okut
                FileManager fm = new FileManager();
                byte[] pieceData = fm.ReadPiece(filePath, pieceIndex);

                // 2. Okunan veriyi P2P mesaj paketinin içine koy
                P2PMessage fileMessage = new P2PMessage
                {
                    Type = MessageType.SendPiece,
                    PieceIndex = pieceIndex,
                    Payload = pieceData
                };

                // 3. Paketi kabloya sığacak formata çevir ve fırlat
                byte[] dataToSend = MessageSerializer.Serialize(fileMessage);
                await _stream.WriteAsync(dataToSend, 0, dataToSend.Length);

                Console.WriteLine($"[BİLGİ] Dosyanın {pieceIndex}. parçası ağ üzerinden fırlatıldı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] Dosya gönderilirken patladık: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _stream?.Close();
            _client?.Close();
        }
    }
}