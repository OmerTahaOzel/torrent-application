using System;
using System.Collections.Generic;
using System.Linq;

namespace Torrent.Core.Download;

public sealed class PieceScheduler
{
    private readonly object _lock = new();
    private readonly bool[] _completed;
    private readonly HashSet<int> _inFlight = new();
    private readonly List<int> _priorityOrder;

    public PieceScheduler(bool[] completedPieces, IReadOnlyList<bool[]> peerBitfields)
    {
        _completed = completedPieces;
        _priorityOrder = BuildRarestFirstOrder(completedPieces.Length, peerBitfields);
    }

    public bool TryAcquirePieceForPeer(bool[] peerBitfield, out int pieceIndex)
    {
        lock (_lock)
        {
            foreach (int idx in _priorityOrder)
            {
                if (_completed[idx] || _inFlight.Contains(idx))
                {
                    continue;
                }

                if (idx >= peerBitfield.Length || !peerBitfield[idx])
                {
                    continue;
                }

                _inFlight.Add(idx);
                pieceIndex = idx;
                return true;
            }

            pieceIndex = -1;
            return false;
        }
    }

    public void MarkCompleted(int pieceIndex)
    {
        lock (_lock)
        {
            _completed[pieceIndex] = true;
            _inFlight.Remove(pieceIndex);
        }
    }

    public void MarkFailed(int pieceIndex)
    {
        lock (_lock)
        {
            _inFlight.Remove(pieceIndex);
        }
    }

    public bool IsCompleted()
    {
        lock (_lock)
        {
            return _completed.All(x => x);
        }
    }

    private static List<int> BuildRarestFirstOrder(int totalPieces, IReadOnlyList<bool[]> peerBitfields)
    {
        int[] rarity = new int[totalPieces];
        foreach (bool[] field in peerBitfields)
        {
            for (int i = 0; i < totalPieces && i < field.Length; i++)
            {
                if (field[i])
                {
                    rarity[i]++;
                }
            }
        }

        return Enumerable.Range(0, totalPieces)
            .OrderBy(i => rarity[i] == 0 ? int.MaxValue : rarity[i])
            .ThenBy(i => i)
            .ToList();
    }
}
