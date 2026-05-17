using Torrent.Core.Models;

namespace Torrent.Core.Runtime;

public sealed class SeededTorrent
{
    public required TorrentMetadata Metadata { get; init; }
    public required byte[][] Pieces { get; init; }
    public required MerkleTree Tree { get; init; }

    public bool[] BuildSeederBitfield()
    {
        bool[] field = new bool[Metadata.TotalPieces];
        for (int i = 0; i < field.Length; i++)
        {
            field[i] = true;
        }

        return field;
    }
}
