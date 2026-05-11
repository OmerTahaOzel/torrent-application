using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Torrent.IO;
using Torrent.Models;

namespace Torrent.Core
{
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

        public static MerkleTree BuildFromFile(string filePath, int pieceSize = FileManager.PieceSize)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Merkle ağacı için kaynak dosya bulunamadı.", filePath);

            List<byte[]> leaves = new List<byte[]>();
            FileManager fileManager = new FileManager();
            int totalPieces = fileManager.GetTotalPieces(filePath, pieceSize);

            for (int i = 0; i < totalPieces; i++)
            {
                byte[] piece = fileManager.ReadPiece(filePath, i, pieceSize);
                leaves.Add(Hash(piece));
            }

            return BuildFromLeafHashes(leaves);
        }

        public static MerkleTree BuildFromLeafHashes(IEnumerable<byte[]> leafHashes)
        {
            List<byte[]> current = leafHashes.Select(x => x.ToArray()).ToList();
            if (current.Count == 0)
            {
                current.Add(Hash(Array.Empty<byte>()));
            }

            int leafCount = current.Count;
            List<List<byte[]>> levels = new List<List<byte[]>> { current };

            while (current.Count > 1)
            {
                List<byte[]> next = new List<byte[]>((current.Count + 1) / 2);
                for (int i = 0; i < current.Count; i += 2)
                {
                    byte[] left = current[i];
                    byte[] right = i + 1 < current.Count ? current[i + 1] : current[i];
                    next.Add(HashConcat(left, right));
                }

                levels.Add(next);
                current = next;
            }

            return new MerkleTree(levels.Select(x => x.Select(y => y.ToArray()).ToList()).ToArray(), leafCount);
        }

        public string[] GetProofHex(int leafIndex)
        {
            if (leafIndex < 0 || leafIndex >= LeafCount)
                throw new ArgumentOutOfRangeException(nameof(leafIndex));

            List<string> proof = new List<string>();
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
            byte[] currentHash = Hash(pieceData);
            int idx = pieceIndex;

            foreach (string siblingHex in proofHex)
            {
                byte[] sibling = Convert.FromHexString(siblingHex);
                currentHash = idx % 2 == 0 ? HashConcat(currentHash, sibling) : HashConcat(sibling, currentHash);
                idx /= 2;
            }

            string calculatedRoot = Convert.ToHexString(currentHash);
            return calculatedRoot.Equals(rootHex, StringComparison.OrdinalIgnoreCase);
        }

        public static FileMetadata BuildMetadata(string filePath, int pieceSize = FileManager.PieceSize)
        {
            FileInfo info = new FileInfo(filePath);
            MerkleTree tree = BuildFromFile(filePath, pieceSize);

            return new FileMetadata
            {
                FileName = info.Name,
                FileSize = info.Length,
                PieceSize = pieceSize,
                TotalPieces = tree.LeafCount,
                MerkleRootHex = tree.RootHex
            };
        }

        private static byte[] Hash(byte[] data)
        {
            return SHA256.HashData(data);
        }

        private static byte[] HashConcat(byte[] left, byte[] right)
        {
            byte[] buffer = new byte[left.Length + right.Length];
            Buffer.BlockCopy(left, 0, buffer, 0, left.Length);
            Buffer.BlockCopy(right, 0, buffer, left.Length, right.Length);
            return Hash(buffer);
        }
    }
}
