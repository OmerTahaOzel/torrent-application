using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Torrent.Core.Models;
using Torrent.Core.Protocol;

Console.Title = "Torrent Tracker";
int port = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 7070;

TcpListener listener = new(IPAddress.Any, port);
listener.Start();
Console.WriteLine($"[TRACKER] Dinleniyor: {port}");

ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedPeer>> table = new(StringComparer.OrdinalIgnoreCase);
CancellationTokenSource cts = new();

_ = Task.Run(() => CleanupLoopAsync(table, cts.Token));

while (!cts.Token.IsCancellationRequested)
{
    TcpClient client = await listener.AcceptTcpClientAsync(cts.Token);
    _ = Task.Run(() => HandleClientAsync(client, table, cts.Token));
}

static async Task HandleClientAsync(
    TcpClient client,
    ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedPeer>> table,
    CancellationToken ct)
{
    using (client)
    using (NetworkStream stream = client.GetStream())
    {
        TrackerResponse response;
        try
        {
            TrackerRequest? request = await WireIO.ReadAsync<TrackerRequest>(stream, ct);
            if (request == null)
            {
                return;
            }

            response = request.Command.ToUpperInvariant() switch
            {
                "REGISTER" => Register(table, request),
                "HEARTBEAT" => Register(table, request),
                "UNREGISTER" => Unregister(table, request),
                "GET_PEERS" => GetPeers(table, request),
                _ => new TrackerResponse { Ok = false, Error = "Bilinmeyen komut" }
            };
        }
        catch (Exception ex)
        {
            response = new TrackerResponse { Ok = false, Error = ex.Message };
        }

        await WireIO.WriteAsync(stream, response, ct);
    }
}

static TrackerResponse Register(
    ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedPeer>> table,
    TrackerRequest request)
{
    if (string.IsNullOrWhiteSpace(request.InfoHash) || string.IsNullOrWhiteSpace(request.PeerId))
    {
        return new TrackerResponse { Ok = false, Error = "InfoHash/PeerId bos olamaz" };
    }

    var peers = table.GetOrAdd(request.InfoHash, _ => new ConcurrentDictionary<string, TrackedPeer>(StringComparer.OrdinalIgnoreCase));
    peers[request.PeerId] = new TrackedPeer
    {
        PeerId = request.PeerId,
        Host = request.Host,
        Port = request.Port,
        Bitfield = request.Bitfield ?? Array.Empty<bool>(),
        LastSeenUtc = DateTime.UtcNow
    };

    return new TrackerResponse { Ok = true };
}

static TrackerResponse Unregister(
    ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedPeer>> table,
    TrackerRequest request)
{
    if (table.TryGetValue(request.InfoHash, out var peers))
    {
        peers.TryRemove(request.PeerId, out _);
    }

    return new TrackerResponse { Ok = true };
}

static TrackerResponse GetPeers(
    ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedPeer>> table,
    TrackerRequest request)
{
    if (!table.TryGetValue(request.InfoHash, out var peers))
    {
        return new TrackerResponse { Ok = true };
    }

    DateTime threshold = DateTime.UtcNow.AddMinutes(-2);
    var results = peers.Values
        .Where(x => x.LastSeenUtc >= threshold && !x.PeerId.Equals(request.PeerId, StringComparison.OrdinalIgnoreCase))
        .Select(x => new PeerEndpoint
        {
            PeerId = x.PeerId,
            Host = x.Host,
            Port = x.Port,
            Bitfield = x.Bitfield
        })
        .ToList();

    return new TrackerResponse { Ok = true, Peers = results };
}

static async Task CleanupLoopAsync(
    ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedPeer>> table,
    CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        DateTime threshold = DateTime.UtcNow.AddMinutes(-2);
        foreach ((string infoHash, var peers) in table.ToArray())
        {
            foreach ((string peerId, TrackedPeer peer) in peers.ToArray())
            {
                if (peer.LastSeenUtc < threshold)
                {
                    peers.TryRemove(peerId, out _);
                }
            }

            if (peers.IsEmpty)
            {
                table.TryRemove(infoHash, out _);
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(30), ct);
    }
}

file sealed class TrackedPeer
{
    public string PeerId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool[] Bitfield { get; set; } = Array.Empty<bool>();
    public DateTime LastSeenUtc { get; set; }
}
