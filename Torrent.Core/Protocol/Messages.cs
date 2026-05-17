using System;
using System.Collections.Generic;
using Torrent.Core.Models;

namespace Torrent.Core.Protocol;

public enum PeerMessageType
{
    Handshake,
    Bitfield,
    RequestPiece,
    PieceData,
    Have,
    KeepAlive,
    MetadataRequest,
    MetadataResponse,
    Error
}

public sealed class PeerMessage
{
    public PeerMessageType Type { get; set; }
    public string InfoHash { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public int PieceIndex { get; set; } = -1;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string[] MerkleProof { get; set; } = Array.Empty<string>();
    public bool[] Bitfield { get; set; } = Array.Empty<bool>();
    public TorrentMetadata? Metadata { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class TrackerRequest
{
    public string Command { get; set; } = string.Empty;
    public string InfoHash { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool[] Bitfield { get; set; } = Array.Empty<bool>();
}

public sealed class TrackerResponse
{
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;
    public List<PeerEndpoint> Peers { get; set; } = new();
}
