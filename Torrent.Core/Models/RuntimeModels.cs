using System;

namespace Torrent.Core.Models;

public sealed class TorrentState
{
    public string InfoHash { get; set; } = string.Empty;
    public string TargetFilePath { get; set; } = string.Empty;
    public bool[] CompletedPieces { get; set; } = Array.Empty<bool>();
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DownloadProgress
{
    public int CompletedPieces { get; set; }
    public int TotalPieces { get; set; }
    public long DownloadedBytes { get; set; }
    public double Percent => TotalPieces == 0 ? 0 : (double)CompletedPieces / TotalPieces * 100.0;
    public int ActivePeers { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class PortAllocationResult
{
    public int RequestedPort { get; init; }
    public int BoundPort { get; init; }
    public bool IsAutoAssigned { get; init; }
    public string? Error { get; init; }
    public bool Success => string.IsNullOrWhiteSpace(Error);
}

public sealed class PeerEndpoint
{
    public string PeerId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool[] Bitfield { get; set; } = Array.Empty<bool>();
}
