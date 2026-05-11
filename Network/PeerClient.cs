using System;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Torrent.Core;
using Torrent.IO;
using Torrent.Models;

namespace Torrent.Network
{
    public class PeerClient
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private string? _sourceFilePath;
        private FileMetadata? _metadata;
        private MerkleTree? _merkleTree;
        private bool _isListening;

        public async Task ConnectToPeerAsync(string ipAddress, int port, string? sourceFilePath = null)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ipAddress, port);
                _stream = _client.GetStream();
                Console.WriteLine($"[BAŞARILI] {ipAddress}:{port} ile bağlantı kuruldu.");

                _sourceFilePath = sourceFilePath;
                if (!string.IsNullOrWhiteSpace(_sourceFilePath))
                {
                    PrepareSeederData(_sourceFilePath);
                    await SendMetadataAsync();
                    await SendBitfieldAsync();
                }

                _isListening = true;
                _ = Task.Run(ListenAsync);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] Bağlantı başarısız: {ex.Message}");
            }
        }

        public async Task SendFilePieceAsync(string filePath, int pieceIndex)
        {
            _sourceFilePath = filePath;
            if (_metadata == null || _merkleTree == null)
            {
                PrepareSeederData(filePath);
            }

            await SendPieceAsync(pieceIndex);
        }

        public void Disconnect()
        {
            _isListening = false;
            _stream?.Close();
            _client?.Close();
        }

        private async Task ListenAsync()
        {
            if (_stream == null)
                return;

            try
            {
                while (_isListening && _client?.Connected == true)
                {
                    P2PMessage? message = await NetworkMessageIO.ReadMessageAsync(_stream);
                    if (message == null)
                        break;

                    await HandleIncomingMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] Dinleme döngüsü hatası: {ex.Message}");
            }
        }

        private async Task HandleIncomingMessageAsync(P2PMessage message)
        {
            switch (message.Type)
            {
                case MessageType.RequestPiece:
                    await SendPieceAsync(message.PieceIndex);
                    break;

                case MessageType.Bitfield:
                    Console.WriteLine("[BİLGİ] Alıcıdan bitfield alındı.");
                    break;

                default:
                    Console.WriteLine($"[BİLGİ] İstemciye mesaj geldi: {message.Type}");
                    break;
            }
        }

        private void PrepareSeederData(string sourceFilePath)
        {
            _metadata = MerkleTree.BuildMetadata(sourceFilePath, FileManager.PieceSize);
            _merkleTree = MerkleTree.BuildFromFile(sourceFilePath, _metadata.PieceSize);
            Console.WriteLine($"[BİLGİ] Seeder metadata hazır. Parça={_metadata.TotalPieces}, Root={_metadata.MerkleRootHex[..12]}...");
        }

        private async Task SendMetadataAsync()
        {
            if (_stream == null || _metadata == null)
                return;

            P2PMessage metadataMessage = new P2PMessage
            {
                Type = MessageType.Metadata,
                PieceIndex = -1,
                Payload = JsonSerializer.SerializeToUtf8Bytes(_metadata)
            };

            await NetworkMessageIO.WriteMessageAsync(_stream, metadataMessage);
            Console.WriteLine("[BİLGİ] Metadata gönderildi.");
        }

        private async Task SendBitfieldAsync()
        {
            if (_stream == null || _metadata == null)
                return;

            byte[] bitfield = Enumerable.Repeat((byte)1, _metadata.TotalPieces).ToArray();
            P2PMessage bitfieldMessage = new P2PMessage
            {
                Type = MessageType.Bitfield,
                PieceIndex = -1,
                Payload = bitfield
            };

            await NetworkMessageIO.WriteMessageAsync(_stream, bitfieldMessage);
            Console.WriteLine("[BİLGİ] Seeder bitfield gönderildi.");
        }

        private async Task SendPieceAsync(int pieceIndex)
        {
            if (_stream == null || _metadata == null || _merkleTree == null || string.IsNullOrWhiteSpace(_sourceFilePath))
            {
                Console.WriteLine("[HATA] Parça gönderimi için seeder hazırlığı eksik.");
                return;
            }

            if (pieceIndex < 0 || pieceIndex >= _metadata.TotalPieces)
            {
                Console.WriteLine($"[HATA] Geçersiz parça indexi: {pieceIndex}");
                return;
            }

            try
            {
                FileManager fileManager = new FileManager();
                byte[] pieceData = fileManager.ReadPiece(_sourceFilePath, pieceIndex, _metadata.PieceSize);

                PieceTransferPayload payload = new PieceTransferPayload
                {
                    Data = pieceData,
                    MerkleProofHex = _merkleTree.GetProofHex(pieceIndex)
                };

                P2PMessage message = new P2PMessage
                {
                    Type = MessageType.SendPiece,
                    PieceIndex = pieceIndex,
                    Payload = JsonSerializer.SerializeToUtf8Bytes(payload)
                };

                await NetworkMessageIO.WriteMessageAsync(_stream, message);
                Console.WriteLine($"[BİLGİ] {pieceIndex}. parça + Merkle proof gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HATA] Parça gönderilemedi: {ex.Message}");
            }
        }
    }
}
