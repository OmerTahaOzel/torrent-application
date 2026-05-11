using System;
using System.IO;

namespace Torrent.Models
{
    public static class MessageSerializer
    {
        // 1. GÖREV: Bizim P2PMessage sınıfını internet kablosuna sığacak Saf Byte dizisine çevirir (Paketleme)
        public static byte[] Serialize(P2PMessage message)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // Sırayla yazıyoruz (Sıra çok önemli, karşı taraf da bu sırayla okuyacak!)
                writer.Write((byte)message.Type);     // 1 byte (Mesaj Tipi)
                writer.Write(message.PieceIndex);     // 4 byte (Parça Numarası)

                // Eğer paketin içinde dosya veya bit listesi (Payload) varsa
                if (message.Payload != null && message.Payload.Length > 0)
                {
                    writer.Write(message.Payload.Length); // Kaç byte veri yolladığımızı söylüyoruz (4 byte)
                    writer.Write(message.Payload);        // Asıl veriyi yapıştırıyoruz
                }
                else
                {
                    writer.Write(0); // Payload yoksa sıfır yazıp geçiyoruz
                }

                return ms.ToArray();
            }
        }

        // 2. GÖREV: İnternetten gelen o anlamsız byte yığınını tekrar bizim P2PMessage sınıfına çevirir (Paket Açma)
        public static P2PMessage Deserialize(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                P2PMessage message = new P2PMessage();

                // Paketlerken hangi sırayla yazdıysak, aynı sırayla okuyoruz
                message.Type = (MessageType)reader.ReadByte();
                message.PieceIndex = reader.ReadInt32();

                int payloadLength = reader.ReadInt32();
                if (payloadLength > 0)
                {
                    message.Payload = reader.ReadBytes(payloadLength);
                }
                else
                {
                    message.Payload = Array.Empty<byte>();
                }

                return message;
            }
        }
    }
}
