using System;

namespace Torrent.Models
{
    public enum MessageType : byte
    {
        Handshake = 0,
        Bitfield = 1,
        RequestPiece = 2,
        SendPiece = 3,
        KeepAlive = 4,
        Metadata = 5
    }

    public class P2PMessage
    {
        public MessageType Type { get; set; }
        public int PieceIndex { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        // YENİ EKLENEN KISIM: Bu veri internetten gelmez, fedai paketi kapıda alırken üzerine yazar amk
        public string SenderIp { get; set; } = string.Empty;
    }
}
