using System;
using System.Collections.Generic;
using System.IO;

namespace Torrent.IO
{
    public class FileManager
    {
        // Seninle konuştuğumuz 1 MB'lık parça boyutu (1024 * 1024 bytes)
        public const int PieceSize = 1048576;

        // 1. GÖREV: Dosyayı parçalara bölüp okumak (Başkasından İstek Gelince)
        public byte[] ReadPiece(string filePath, int pieceIndex, int pieceSize = PieceSize)
        {
            // Dosya var mı yok mu kontrolü, yoksa patlamayalım amk
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Ağa dosya ortada yok amk!");

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // Okunacak parçanın başlangıç noktasına (offset) atla
                long offset = (long)pieceIndex * pieceSize;
                if (offset >= fs.Length)
                    throw new ArgumentOutOfRangeException(nameof(pieceIndex), "İstenen parça dosya sınırları dışında.");

                fs.Seek(offset, SeekOrigin.Begin);

                // Son parça 1 MB'dan küçük olabilir, onu hesaplıyoruz
                int bytesToRead = (int)Math.Min(pieceSize, fs.Length - offset);
                byte[] buffer = new byte[bytesToRead];

                // Parçayı belleğe al ve geri dön
                fs.Read(buffer, 0, bytesToRead);
                return buffer;
            }
        }

        // 2. GÖREV: Gelen parçayı diske doğru yere yazmak (İndirme Yaparken)
        public void WritePiece(string filePath, int pieceIndex, byte[] data, int pieceSize = PieceSize)
        {
            // İndirilen dosyayı oluştur veya varsa üstüne ekle
            using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
            {
                // Yazılacak parçanın koordinatını bul
                long offset = (long)pieceIndex * pieceSize;
                fs.Seek(offset, SeekOrigin.Begin);

                // Çat diye diske yaz
                fs.Write(data, 0, data.Length);
                Console.WriteLine($"[BAŞARILI] {pieceIndex}. parça diske yazıldı kumandanım!");
            }
        }

        public int GetTotalPieces(string filePath, int pieceSize = PieceSize)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Parça sayısı hesaplanırken dosya bulunamadı.", filePath);

            long fileSize = new FileInfo(filePath).Length;
            if (fileSize == 0)
                return 1;

            return (int)Math.Ceiling((double)fileSize / pieceSize);
        }
    }
}
