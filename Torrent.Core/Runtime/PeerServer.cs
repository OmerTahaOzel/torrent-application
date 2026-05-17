using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Torrent.Core.Models;
using Torrent.Core.Protocol;

namespace Torrent.Core.Runtime;

public sealed class PeerServer : IAsyncDisposable
{
    private readonly Func<string, SeededTorrent?> _resolveSeed;
    private readonly Action<string>? _log;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int BoundPort { get; private set; }

    public PeerServer(Func<string, SeededTorrent?> resolveSeed, Action<string>? log = null)
    {
        _resolveSeed = resolveSeed;
        _log = log;
    }

    public PortAllocationResult Start(int requestedPort)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, requestedPort);
            _listener.Start();
            BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));

            return new PortAllocationResult
            {
                RequestedPort = requestedPort,
                BoundPort = BoundPort,
                IsAutoAssigned = requestedPort == 0
            };
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return new PortAllocationResult
            {
                RequestedPort = requestedPort,
                BoundPort = 0,
                IsAutoAssigned = requestedPort == 0,
                Error = "Port kullanimda"
            };
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        if (_listener == null)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Accept hatasi: {ex.Message}");
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            Channel<PeerMessage> outbound = Channel.CreateUnbounded<PeerMessage>();
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Task sendLoop = Task.Run(async () =>
            {
                try
                {
                    while (await outbound.Reader.WaitToReadAsync(linkedCts.Token))
                    {
                        while (outbound.Reader.TryRead(out PeerMessage? msg))
                        {
                            await WireIO.WriteAsync(stream, msg, linkedCts.Token);
                        }
                    }
                }
                catch
                {
                }
            }, linkedCts.Token);

            string activeInfoHash = string.Empty;
            SeededTorrent? seed = null;

            try
            {
                while (!linkedCts.IsCancellationRequested)
                {
                    PeerMessage? message = await WireIO.ReadAsync<PeerMessage>(stream, linkedCts.Token);
                    if (message == null)
                    {
                        break;
                    }

                    switch (message.Type)
                    {
                        case PeerMessageType.Handshake:
                            activeInfoHash = message.InfoHash;
                            seed = _resolveSeed(activeInfoHash);
                            if (seed == null)
                            {
                                await outbound.Writer.WriteAsync(new PeerMessage
                                {
                                    Type = PeerMessageType.Error,
                                    Error = "InfoHash bulunamadi"
                                }, linkedCts.Token);
                            }
                            else
                            {
                                await outbound.Writer.WriteAsync(new PeerMessage
                                {
                                    Type = PeerMessageType.Bitfield,
                                    InfoHash = activeInfoHash,
                                    Bitfield = seed.BuildSeederBitfield()
                                }, linkedCts.Token);
                            }
                            break;

                        case PeerMessageType.MetadataRequest:
                            if (seed != null)
                            {
                                await outbound.Writer.WriteAsync(new PeerMessage
                                {
                                    Type = PeerMessageType.MetadataResponse,
                                    InfoHash = activeInfoHash,
                                    Metadata = seed.Metadata
                                }, linkedCts.Token);
                            }
                            break;

                        case PeerMessageType.RequestPiece:
                            if (seed == null)
                            {
                                break;
                            }

                            int idx = message.PieceIndex;
                            if (idx < 0 || idx >= seed.Pieces.Length)
                            {
                                break;
                            }

                            await outbound.Writer.WriteAsync(new PeerMessage
                            {
                                Type = PeerMessageType.PieceData,
                                InfoHash = activeInfoHash,
                                PieceIndex = idx,
                                Data = seed.Pieces[idx],
                                MerkleProof = seed.Tree.GetProofHex(idx)
                            }, linkedCts.Token);
                            break;

                        case PeerMessageType.KeepAlive:
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Peer handler hatasi: {ex.Message}");
            }
            finally
            {
                outbound.Writer.TryComplete();
                linkedCts.Cancel();
                try { await sendLoop; } catch { }
                linkedCts.Dispose();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }
}
