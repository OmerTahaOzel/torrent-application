using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Torrent.Core.Models;

namespace Torrent.Core.Runtime;

public sealed class MerkleTree
{
    private readonly List<byte[]>[] _levels;
    public int LeafCount { get; }
    public string RootHex => Convert.ToHexString(_levels[^1][0]);

    private MerkleTree(List<byte[]>[] levels, int leafCount)
    {
        _levels = levels;
        LeafCount = leafCount;
    }

    public static MerkleTree BuildFromLeafHashes(IReadOnlyList<byte[]> leafHashes)
    {
        List<byte[]> current = leafHashes.Select(x => x.ToArray()).ToList();
        if (current.Count == 0)
        {
            current.Add(SHA256.HashData(Array.Empty<byte>()));
        }

        int leafCount = current.Count;
        List<List<byte[]>> levels = new() { current };

        while (current.Count > 1)
        {
            List<byte[]> next = new((current.Count + 1) / 2);
            for (int i = 0; i < current.Count; i += 2)
            {
                byte[] left = current[i];
                byte[] right = i + 1 < current.Count ? current[i + 1] : current[i];
                next.Add(HashConcat(left, right));
            }

            levels.Add(next);
            current = next;
        }

        return new MerkleTree(levels.Select(l => l.Select(x => x.ToArray()).ToList()).ToArray(), leafCount);
    }

    public string[] GetProofHex(int leafIndex)
    {
        if (leafIndex < 0 || leafIndex >= LeafCount)
        {
            throw new ArgumentOutOfRangeException(nameof(leafIndex));
        }

        List<string> proof = new();
        int idx = leafIndex;
        for (int levelIndex = 0; levelIndex < _levels.Length - 1; levelIndex++)
        {
            List<byte[]> level = _levels[levelIndex];
            int siblingIndex = idx % 2 == 0 ? idx + 1 : idx - 1;
            if (siblingIndex >= level.Count)
            {
                siblingIndex = idx;
            }

            proof.Add(Convert.ToHexString(level[siblingIndex]));
            idx /= 2;
        }

        return proof.ToArray();
    }

    public static bool VerifyPiece(byte[] pieceData, int pieceIndex, string rootHex, IReadOnlyList<string> proofHex)
    {
        byte[] hash = SHA256.HashData(pieceData);
        int idx = pieceIndex;
        foreach (string siblingHex in proofHex)
        {
            byte[] sibling = Convert.FromHexString(siblingHex);
            hash = idx % 2 == 0 ? HashConcat(hash, sibling) : HashConcat(sibling, hash);
            idx /= 2;
        }

        return Convert.ToHexString(hash).Equals(rootHex, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] HashConcat(byte[] left, byte[] right)
    {
        byte[] data = new byte[left.Length + right.Length];
        Buffer.BlockCopy(left, 0, data, 0, left.Length);
        Buffer.BlockCopy(right, 0, data, left.Length, right.Length);
        return SHA256.HashData(data);
    }
}

public static class TorrentMetadataBuilder
{
    public const int DefaultPieceSize = 1024 * 1024;

    public static (TorrentMetadata Metadata, byte[][] Pieces, MerkleTree Tree) BuildFromFile(string filePath, int pieceSize = DefaultPieceSize)
    {
        FileInfo info = new(filePath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Kaynak dosya bulunamadi.", filePath);
        }

        List<byte[]> pieces = new();
        List<byte[]> hashes = new();

        using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] buffer = new byte[pieceSize];
        while (true)
        {
            int read = fs.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            byte[] piece = new byte[read];
            Buffer.BlockCopy(buffer, 0, piece, 0, read);
            pieces.Add(piece);
            hashes.Add(SHA256.HashData(piece));
        }

        if (pieces.Count == 0)
        {
            pieces.Add(Array.Empty<byte>());
            hashes.Add(SHA256.HashData(Array.Empty<byte>()));
        }

        MerkleTree tree = MerkleTree.BuildFromLeafHashes(hashes);
        string[] pieceHashes = hashes.Select(Convert.ToHexString).ToArray();

        TorrentMetadata metadata = new()
        {
            FileName = info.Name,
            FileSize = info.Length,
            PieceSize = pieceSize,
            TotalPieces = pieces.Count,
            MerkleRoot = tree.RootHex,
            PieceHashes = pieceHashes
        };

        metadata.InfoHash = BuildInfoHash(metadata);
        return (metadata, pieces.ToArray(), tree);
    }

    public static string BuildInfoHash(TorrentMetadata metadata)
    {
        var canon = new
        {
            metadata.FileName,
            metadata.FileSize,
            metadata.PieceSize,
            metadata.TotalPieces,
            metadata.MerkleRoot,
            metadata.PieceHashes
        };

        string json = JsonSerializer.Serialize(canon);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}
