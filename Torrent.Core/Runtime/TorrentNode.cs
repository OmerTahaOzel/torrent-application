using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Torrent.Core.Download;
using Torrent.Core.IO;
using Torrent.Core.Models;
using Torrent.Core.Protocol;
using Torrent.Core.Tracker;

namespace Torrent.Core.Runtime;

public sealed class TorrentNode : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, SeededTorrent> _seeded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string>? _log;
    private PeerServer? _peerServer;
    private TrackerClient? _tracker;
    private CancellationTokenSource? _heartbeatCts;
    private readonly List<(string InfoHash, Func<bool[]> BitfieldFactory)> _announced = new();

    public string PeerId { get; } = Guid.NewGuid().ToString("N");
    public int BoundPort => _peerServer?.BoundPort ?? 0;
    public string LocalHostForTracker { get; private set; } = "127.0.0.1";

    public TorrentNode(Action<string>? log = null)
    {
        _log = log;
    }

    public PortAllocationResult Start(string trackerHost, int trackerPort, int requestedPort, string localHostForTracker = "127.0.0.1")
    {
        _tracker = new TrackerClient(trackerHost, trackerPort);
        LocalHostForTracker = localHostForTracker;

        _peerServer = new PeerServer(ResolveSeed, _log);
        PortAllocationResult result = _peerServer.Start(requestedPort);
        if (!result.Success)
        {
            return result;
        }

        _heartbeatCts = new CancellationTokenSource();
        _ = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));

        return result;
    }

    public async Task<string> StartSeedingAsync(string sourceFilePath, CancellationToken ct = default)
    {
        EnsureStarted();
        var (metadata, pieces, tree) = TorrentMetadataBuilder.BuildFromFile(sourceFilePath);
        string metadataPath = MetadataStore.Save(sourceFilePath, metadata);

        SeededTorrent seed = new()
        {
            Metadata = metadata,
            Pieces = pieces,
            Tree = tree
        };

        _seeded[metadata.InfoHash] = seed;
        _announced.RemoveAll(x => x.InfoHash.Equals(metadata.InfoHash, StringComparison.OrdinalIgnoreCase));
        _announced.Add((metadata.InfoHash, seed.BuildSeederBitfield));

        await _tracker!.RegisterAsync(
            metadata.InfoHash,
            PeerId,
            LocalHostForTracker,
            BoundPort,
            seed.BuildSeederBitfield(),
            ct);

        _log?.Invoke($"Seeding basladi: {metadata.FileName} ({metadata.TotalPieces} parca)");
        return metadataPath;
    }

    public async Task DownloadAsync(
        string metadataPath,
        string outputDirectory,
        IProgress<DownloadProgress>? progress = null,
        int maxParallelPeers = 4,
        TimeSpan? pieceTimeout = null,
        CancellationToken ct = default)
    {
        EnsureStarted();

        TorrentMetadata metadata = MetadataStore.Load(metadataPath);
        string targetFilePath = Path.Combine(outputDirectory, metadata.FileName);
        TorrentState state = StateStore.LoadOrCreate(targetFilePath, metadata.InfoHash, metadata.TotalPieces);
        StateStore.Save(state);

        IReadOnlyList<PeerEndpoint> endpoints = await _tracker!.GetPeersAsync(metadata.InfoHash, PeerId, ct);
        List<PeerEndpoint> remotePeers = endpoints
            .Where(p => !p.PeerId.Equals(PeerId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (remotePeers.Count == 0)
        {
            throw new InvalidOperationException("Tracker uzerinde uygun peer bulunamadi.");
        }

        List<PeerSession> sessions = new();
        try
        {
            foreach (PeerEndpoint endpoint in remotePeers.Take(Math.Max(1, maxParallelPeers)))
            {
                PeerSession session = new(endpoint);
                try
                {
                    await session.ConnectAsync(metadata.InfoHash, PeerId, ct);
                    sessions.Add(session);
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"Peer baglantisi basarisiz {endpoint.Host}:{endpoint.Port} -> {ex.Message}");
                    await session.DisposeAsync();
                }
            }

            if (sessions.Count == 0)
            {
                throw new InvalidOperationException("Peer baglantisi kurulamadigi icin indirme baslayamadi.");
            }

            PieceScheduler scheduler = new(state.CompletedPieces, sessions.Select(s => s.Bitfield).ToList());
            using PieceFileStore fileStore = new(targetFilePath, metadata.FileSize, metadata.PieceSize);

            TimeSpan timeout = pieceTimeout ?? TimeSpan.FromSeconds(10);
            List<Task> workers = sessions.Select(s => RunPeerWorkerAsync(s, metadata, scheduler, state, fileStore, timeout, progress, ct)).ToList();
            await Task.WhenAll(workers);

            if (!scheduler.IsCompleted())
            {
                throw new InvalidOperationException("Tum parcalar tamamlanamadi.");
            }

            VerifyFinalHash(metadata, targetFilePath);
            progress?.Report(BuildProgress(state, metadata.TotalPieces, sessions.Count, "Tamamlandi"));
        }
        finally
        {
            foreach (PeerSession session in sessions)
            {
                await session.DisposeAsync();
            }
        }
    }

    private async Task RunPeerWorkerAsync(
        PeerSession session,
        TorrentMetadata metadata,
        PieceScheduler scheduler,
        TorrentState state,
        PieceFileStore store,
        TimeSpan timeout,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !scheduler.IsCompleted())
        {
            if (!scheduler.TryAcquirePieceForPeer(session.Bitfield, out int pieceIndex))
            {
                await Task.Delay(150, ct);
                continue;
            }

            PeerMessage? pieceMsg = await session.RequestPieceAsync(metadata.InfoHash, pieceIndex, timeout, ct);
            if (pieceMsg == null || pieceMsg.Data.Length == 0)
            {
                scheduler.MarkFailed(pieceIndex);
                continue;
            }

            if (!VerifyPiece(metadata, pieceMsg, pieceIndex))
            {
                scheduler.MarkFailed(pieceIndex);
                continue;
            }

            store.WritePiece(pieceIndex, pieceMsg.Data);
            scheduler.MarkCompleted(pieceIndex);
            state.CompletedPieces[pieceIndex] = true;
            StateStore.Save(state);

            progress?.Report(BuildProgress(state, metadata.TotalPieces, 0, $"Parca {pieceIndex} tamamlandi"));
        }
    }

    private static DownloadProgress BuildProgress(TorrentState state, int totalPieces, int activePeers, string status)
    {
        int complete = state.CompletedPieces.Count(x => x);
        return new DownloadProgress
        {
            CompletedPieces = complete,
            TotalPieces = totalPieces,
            DownloadedBytes = complete,
            ActivePeers = activePeers,
            Status = status
        };
    }

    private static bool VerifyPiece(TorrentMetadata metadata, PeerMessage pieceMsg, int pieceIndex)
    {
        string hash = Convert.ToHexString(SHA256.HashData(pieceMsg.Data));
        if (!hash.Equals(metadata.PieceHashes[pieceIndex], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return MerkleTree.VerifyPiece(pieceMsg.Data, pieceIndex, metadata.MerkleRoot, pieceMsg.MerkleProof);
    }

    private static void VerifyFinalHash(TorrentMetadata metadata, string targetFilePath)
    {
        List<byte[]> pieceHashes = new();
        using FileStream fs = new(targetFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] buffer = new byte[metadata.PieceSize];
        while (true)
        {
            int read = fs.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            byte[] piece = new byte[read];
            Buffer.BlockCopy(buffer, 0, piece, 0, read);
            pieceHashes.Add(SHA256.HashData(piece));
        }

        if (pieceHashes.Count == 0)
        {
            pieceHashes.Add(SHA256.HashData(Array.Empty<byte>()));
        }

        MerkleTree tree = MerkleTree.BuildFromLeafHashes(pieceHashes);
        if (!tree.RootHex.Equals(metadata.MerkleRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Final dosya Merkle root dogrulamasindan gecmedi.");
        }
    }

    private SeededTorrent? ResolveSeed(string infoHash)
    {
        _seeded.TryGetValue(infoHash, out SeededTorrent? seed);
        return seed;
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var item in _announced.ToArray())
                {
                    await _tracker!.HeartbeatAsync(item.InfoHash, PeerId, LocalHostForTracker, BoundPort, item.BitfieldFactory(), ct);
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Heartbeat hatasi: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void EnsureStarted()
    {
        if (_peerServer == null || _tracker == null)
        {
            throw new InvalidOperationException("Node once Start ile baslatilmalidir.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _heartbeatCts?.Cancel();

        if (_tracker != null)
        {
            foreach (var item in _announced.ToArray())
            {
                try
                {
                    await _tracker.UnregisterAsync(item.InfoHash, PeerId);
                }
                catch
                {
                }
            }
        }

        _announced.Clear();
        _heartbeatCts?.Dispose();

        if (_peerServer != null)
        {
            await _peerServer.DisposeAsync();
        }
    }
}
