using System;

namespace Torrent.Models
{
    public class FileMetadata
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int PieceSize { get; set; }
        public int TotalPieces { get; set; }
        public string MerkleRootHex { get; set; } = string.Empty;
    }

    public class PieceTransferPayload
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string[] MerkleProofHex { get; set; } = Array.Empty<string>();
    }
}
