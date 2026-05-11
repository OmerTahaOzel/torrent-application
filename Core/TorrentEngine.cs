using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Torrent.IO;
using Torrent.Models;

namespace Torrent.Core
{
    public class TorrentEngine
    {
        private readonly Dictionary<string, Peer> _connectedPeers = new Dictionary<string, Peer>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _requestedPieces = new HashSet<int>();
        private bool[] _myBitfield;
        private int _totalPieces;
        private FileMetadata? _activeMetadata;
        private readonly string _downloadTargetFileName = "Karabuk_P2P_Indirilen.txt";

        public TorrentEngine(int totalPieces)
        {
            _totalPieces = Math.Max(1, totalPieces);
            _myBitfield = new bool[_totalPieces];
        }

        public FileMetadata? ActiveMetadata => _activeMetadata;
        public bool HasAllPieces => _myBitfield.All(x => x);

        public void SetActiveMetadata(FileMetadata metadata)
        {
            _activeMetadata = metadata;
            ResizeBitfield(metadata.TotalPieces);
        }

        public void AddPeer(string ipAddress, int port)
        {
            if (!_connectedPeers.ContainsKey(ipAddress))
            {
                _connectedPeers[ipAddress] = new Peer(ipAddress, port, _totalPieces);
            }

            Console.WriteLine($"[MOTOR] {ipAddress}:{port} ağa eklendi. Toplam Eş Sayısı: {_connectedPeers.Count}");
        }

        public void ProcessMessage(P2PMessage message, string senderIp)
        {
            switch (message.Type)
            {
                case MessageType.Handshake:
                case MessageType.Metadata:
                    HandleMetadataMessage(message, senderIp);
                    break;

                case MessageType.Bitfield:
                    HandleBitfieldMessage(message, senderIp);
                    break;

                case MessageType.RequestPiece:
                    _requestedPieces.Add(message.PieceIndex);
                    Console.WriteLine($"[MOTOR] {senderIp} {message.PieceIndex}. parçayı istedi.");
                    break;

                case MessageType.SendPiece:
                    HandleIncomingPiece(message, senderIp);
                    break;

                case MessageType.KeepAlive:
                    Console.WriteLine($"[MOTOR] {senderIp} keep-alive gönderdi.");
                    break;

                default:
                    Console.WriteLine($"[MOTOR] Bilinmeyen mesaj tipi: {message.Type}");
                    break;
            }
        }

        public P2PMessage CreateBitfieldMessage()
        {
            byte[] payload = _myBitfield.Select(x => x ? (byte)1 : (byte)0).ToArray();
            return new P2PMessage
            {
                Type = MessageType.Bitfield,
                PieceIndex = -1,
                Payload = payload
            };
        }

        public IEnumerable<int> GetMissingPieceIndexes()
        {
            for (int i = 0; i < _myBitfield.Length; i++)
            {
                if (!_myBitfield[i])
                    yield return i;
            }
        }

        private void HandleMetadataMessage(P2PMessage message, string senderIp)
        {
            Console.WriteLine($"[MOTOR] {senderIp} ile metadata/handshake alındı.");

            if (message.Payload.Length == 0)
                return;

            try
            {
                FileMetadata? metadata = JsonSerializer.Deserialize<FileMetadata>(message.Payload);
                if (metadata == null || metadata.TotalPieces <= 0 || string.IsNullOrWhiteSpace(metadata.MerkleRootHex))
                {
                    Console.WriteLine("[MOTOR] Metadata içeriği geçersiz.");
                    return;
                }

                SetActiveMetadata(metadata);

                if (_connectedPeers.TryGetValue(senderIp, out Peer? peer))
                {
                    peer.MerkleRootHex = metadata.MerkleRootHex;
                }

                Console.WriteLine($"[MOTOR] Dosya: {metadata.FileName}, Parça: {metadata.TotalPieces}, MerkleRoot: {metadata.MerkleRootHex[..12]}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MOTOR] Metadata parse hatası: {ex.Message}");
            }
        }

        private void HandleBitfieldMessage(P2PMessage message, string senderIp)
        {
            Console.WriteLine($"[MOTOR] {senderIp} adlı eşten parça haritası geldi.");
            bool[] incomingBitfield = message.Payload.Select(x => x == 1).ToArray();

            if (_connectedPeers.TryGetValue(senderIp, out Peer? peer))
            {
                if (peer.AvailablePieces.Length != incomingBitfield.Length)
                {
                    peer.AvailablePieces = new bool[incomingBitfield.Length];
                }

                peer.UpdateBitfield(incomingBitfield);
            }
        }

        private void HandleIncomingPiece(P2PMessage message, string senderIp)
        {
            Console.WriteLine($"[MOTOR] {senderIp} {message.PieceIndex}. parçayı gönderdi.");

            try
            {
                PieceTransferPayload? transfer = JsonSerializer.Deserialize<PieceTransferPayload>(message.Payload);
                if (transfer == null || transfer.Data.Length == 0)
                {
                    Console.WriteLine("[MOTOR] Gelen parça payload boş veya bozuk.");
                    return;
                }

                if (_activeMetadata == null)
                {
                    Console.WriteLine("[MOTOR] Metadata bilinmediği için parça doğrulanamadı.");
                    return;
                }

                if (message.PieceIndex < 0 || message.PieceIndex >= _activeMetadata.TotalPieces)
                {
                    Console.WriteLine($"[MOTOR] Geçersiz parça indexi: {message.PieceIndex}");
                    return;
                }

                bool isValid = MerkleTree.VerifyPiece(
                    transfer.Data,
                    message.PieceIndex,
                    _activeMetadata.MerkleRootHex,
                    transfer.MerkleProofHex);

                if (!isValid)
                {
                    Console.WriteLine($"[MOTOR] {message.PieceIndex}. parça Merkle doğrulamasından geçemedi, yazılmadı.");
                    return;
                }

                FileManager fileManager = new FileManager();
                fileManager.WritePiece(_downloadTargetFileName, message.PieceIndex, transfer.Data, _activeMetadata.PieceSize);
                _myBitfield[message.PieceIndex] = true;

                Console.WriteLine($"[MOTOR] {message.PieceIndex}. parça doğrulandı ve diske yazıldı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MOTOR] Parça işleme hatası: {ex.Message}");
            }
        }

        private void ResizeBitfield(int totalPieces)
        {
            if (totalPieces <= 0 || totalPieces == _totalPieces)
                return;

            bool[] resized = new bool[totalPieces];
            Array.Copy(_myBitfield, resized, Math.Min(_myBitfield.Length, resized.Length));
            _myBitfield = resized;
            _totalPieces = totalPieces;
        }
    }
}
