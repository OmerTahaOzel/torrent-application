using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Torrent.Models;

namespace Torrent.Network
{
    public static class NetworkMessageIO
    {
        public static async Task WriteMessageAsync(NetworkStream stream, P2PMessage message)
        {
            byte[] payload = MessageSerializer.Serialize(message);
            byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
            await stream.WriteAsync(payload, 0, payload.Length);
        }

        public static async Task<P2PMessage?> ReadMessageAsync(NetworkStream stream)
        {
            byte[] lengthBuffer = new byte[sizeof(int)];
            bool hasLength = await ReadExactAsync(stream, lengthBuffer, lengthBuffer.Length);
            if (!hasLength)
                return null;

            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (messageLength <= 0 || messageLength > 100 * 1024 * 1024)
                throw new InvalidDataException($"Geçersiz mesaj boyutu: {messageLength}");

            byte[] payload = new byte[messageLength];
            bool hasPayload = await ReadExactAsync(stream, payload, messageLength);
            if (!hasPayload)
                return null;

            return MessageSerializer.Deserialize(payload);
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int totalBytes)
        {
            int read = 0;
            while (read < totalBytes)
            {
                int current = await stream.ReadAsync(buffer, read, totalBytes - read);
                if (current == 0)
                    return false;

                read += current;
            }

            return true;
        }
    }
}
