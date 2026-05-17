using System;

namespace Torrent.Core.Models;

public sealed class TorrentMetadata
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int PieceSize { get; set; }
    public int TotalPieces { get; set; }
    public string InfoHash { get; set; } = string.Empty;
    public string MerkleRoot { get; set; } = string.Empty;
    public string[] PieceHashes { get; set; } = Array.Empty<string>();
}
