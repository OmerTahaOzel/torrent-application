using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Torrent.Core.Models;
using Torrent.Core.Protocol;

namespace Torrent.Core.Tracker;

public sealed class TrackerClient
{
    private readonly string _host;
    private readonly int _port;

    public TrackerClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public Task RegisterAsync(string infoHash, string peerId, string host, int port, bool[] bitfield, CancellationToken ct = default)
        => SendSimpleAsync(new TrackerRequest
        {
            Command = "REGISTER",
            InfoHash = infoHash,
            PeerId = peerId,
            Host = host,
            Port = port,
            Bitfield = bitfield
        }, ct);

    public Task HeartbeatAsync(string infoHash, string peerId, string host, int port, bool[] bitfield, CancellationToken ct = default)
        => SendSimpleAsync(new TrackerRequest
        {
            Command = "HEARTBEAT",
            InfoHash = infoHash,
            PeerId = peerId,
            Host = host,
            Port = port,
            Bitfield = bitfield
        }, ct);

    public Task UnregisterAsync(string infoHash, string peerId, CancellationToken ct = default)
        => SendSimpleAsync(new TrackerRequest
        {
            Command = "UNREGISTER",
            InfoHash = infoHash,
            PeerId = peerId
        }, ct);

    public async Task<IReadOnlyList<PeerEndpoint>> GetPeersAsync(string infoHash, string requesterPeerId, CancellationToken ct = default)
    {
        TrackerResponse response = await SendAsync(new TrackerRequest
        {
            Command = "GET_PEERS",
            InfoHash = infoHash,
            PeerId = requesterPeerId
        }, ct);

        if (!response.Ok)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response.Peers;
    }

    private async Task SendSimpleAsync(TrackerRequest request, CancellationToken ct)
    {
        TrackerResponse response = await SendAsync(request, ct);
        if (!response.Ok)
        {
            throw new InvalidOperationException(response.Error);
        }
    }

    private async Task<TrackerResponse> SendAsync(TrackerRequest request, CancellationToken ct)
    {
        using TcpClient client = new();
        await client.ConnectAsync(_host, _port, ct);
        using NetworkStream stream = client.GetStream();

        await WireIO.WriteAsync(stream, request, ct);
        TrackerResponse? response = await WireIO.ReadAsync<TrackerResponse>(stream, ct);
        if (response == null)
        {
            throw new InvalidOperationException("Tracker cevap vermedi.");
        }

        return response;
    }
}
