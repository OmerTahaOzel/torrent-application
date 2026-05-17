using System;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Torrent.Core.Protocol;

public static class WireIO
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    //4 byte length prefix + JSON payload
    public static async Task WriteAsync<T>(NetworkStream stream, T message, CancellationToken cancellationToken = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        byte[] len = BitConverter.GetBytes(payload.Length);
        await stream.WriteAsync(len, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
    }

    public static async Task<T?> ReadAsync<T>(NetworkStream stream, CancellationToken cancellationToken = default)
    {
        byte[] lenBuffer = new byte[4];
        bool hasLen = await ReadExactAsync(stream, lenBuffer, cancellationToken);
        if (!hasLen)
        {
            return default;
        }

        int len = BitConverter.ToInt32(lenBuffer, 0);
        if (len <= 0 || len > 256 * 1024 * 1024)
        {
            throw new InvalidDataException($"Gecersiz paket boyutu: {len}");
        }

        byte[] payload = new byte[len];
        bool hasPayload = await ReadExactAsync(stream, payload, cancellationToken);
        if (!hasPayload)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
