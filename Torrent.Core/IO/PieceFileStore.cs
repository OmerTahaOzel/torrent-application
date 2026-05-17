using System;
using System.IO;

namespace Torrent.Core.IO;

public sealed class PieceFileStore : IDisposable
{
    private readonly FileStream _stream;
    private readonly int _pieceSize;

    public PieceFileStore(string path, long totalFileSize, int pieceSize)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);
        _stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        _pieceSize = pieceSize;
        if (_stream.Length != totalFileSize)
        {
            _stream.SetLength(totalFileSize);
        }
    }

    public byte[] ReadPiece(int pieceIndex, int expectedLength)
    {
        long offset = (long)pieceIndex * _pieceSize;
        byte[] buffer = new byte[expectedLength];
        _stream.Position = offset;
        int read = 0;
        while (read < expectedLength)
        {
            int n = _stream.Read(buffer, read, expectedLength - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        if (read == expectedLength)
        {
            return buffer;
        }

        byte[] trimmed = new byte[read];
        Buffer.BlockCopy(buffer, 0, trimmed, 0, read);
        return trimmed;
    }

    public void WritePiece(int pieceIndex, byte[] data)
    {
        long offset = (long)pieceIndex * _pieceSize;
        _stream.Position = offset;
        _stream.Write(data, 0, data.Length);
        _stream.Flush();
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
