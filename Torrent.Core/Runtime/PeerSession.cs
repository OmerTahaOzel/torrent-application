using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Torrent.Core.Models;
using Torrent.Core.Protocol;

namespace Torrent.Core.Runtime;

public sealed class PeerSession : IAsyncDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private readonly Channel<PeerMessage> _outbound = Channel.CreateUnbounded<PeerMessage>();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<PeerMessage>> _pieceWaiters = new();
    private readonly TaskCompletionSource<bool[]> _bitfieldTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _cts;
    private Task? _sendLoop;
    private Task? _recvLoop;

    public string PeerId { get; }
    public PeerEndpoint Endpoint { get; }
    public bool[] Bitfield { get; private set; } = Array.Empty<bool>();
    public bool IsConnected => _client.Connected;

    public PeerSession(PeerEndpoint endpoint)
    {
        Endpoint = endpoint;
        PeerId = endpoint.PeerId;
    }

    public async Task ConnectAsync(string infoHash, string myPeerId, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await _client.ConnectAsync(Endpoint.Host, Endpoint.Port, ct);
        _stream = _client.GetStream();

        _sendLoop = Task.Run(() => SendLoopAsync(_cts.Token), _cts.Token);
        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

        await SendAsync(new PeerMessage
        {
            Type = PeerMessageType.Handshake,
            InfoHash = infoHash,
            PeerId = myPeerId
        }, ct);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        Bitfield = await _bitfieldTcs.Task.WaitAsync(timeout.Token);
    }

    public async Task SendAsync(PeerMessage message, CancellationToken ct)
    {
        await _outbound.Writer.WriteAsync(message, ct);
    }

    public async Task<PeerMessage?> RequestPieceAsync(string infoHash, int pieceIndex, TimeSpan timeout, CancellationToken ct)
    {
        TaskCompletionSource<PeerMessage> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pieceWaiters.TryAdd(pieceIndex, waiter))
        {
            return null;
        }

        await SendAsync(new PeerMessage
        {
            Type = PeerMessageType.RequestPiece,
            InfoHash = infoHash,
            PieceIndex = pieceIndex
        }, ct);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await waiter.Task.WaitAsync(timeoutCts.Token);
        }
        catch
        {
            _pieceWaiters.TryRemove(pieceIndex, out _);
            return null;
        }
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        if (_stream == null)
        {
            return;
        }

        try
        {
            while (await _outbound.Reader.WaitToReadAsync(ct))
            {
                while (_outbound.Reader.TryRead(out PeerMessage? msg))
                {
                    await WireIO.WriteAsync(_stream, msg, ct);
                }
            }
        }
        catch
        {
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_stream == null)
        {
            return;
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                PeerMessage? msg = await WireIO.ReadAsync<PeerMessage>(_stream, ct);
                if (msg == null)
                {
                    break;
                }

                if (msg.Type == PeerMessageType.Bitfield)
                {
                    Bitfield = msg.Bitfield;
                    _bitfieldTcs.TrySetResult(Bitfield);
                }
                else if (msg.Type == PeerMessageType.PieceData)
                {
                    if (_pieceWaiters.TryRemove(msg.PieceIndex, out TaskCompletionSource<PeerMessage>? waiter))
                    {
                        waiter.TrySetResult(msg);
                    }
                }
            }
        }
        catch
        {
            _bitfieldTcs.TrySetCanceled();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _outbound.Writer.TryComplete();

        if (_sendLoop != null)
        {
            try { await _sendLoop; } catch { }
        }

        if (_recvLoop != null)
        {
            try { await _recvLoop; } catch { }
        }

        _stream?.Close();
        _client.Close();
        _cts?.Dispose();
    }
}
