using System;

namespace Torrent.Models
{
    public class Peer
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }

        // Elemanın elindeki dosya parçalarının haritası (True: Var, False: Yok)
        public bool[] AvailablePieces { get; set; }
        public string MerkleRootHex { get; set; } = string.Empty;

        public Peer(string ipAddress, int port, int totalPiecesInFile)
        {
            IpAddress = ipAddress;
            Port = port;

            // Dosya toplam kaç parçaysa, o kadar uzunlukta bir harita (Bitfield) açıyoruz
            AvailablePieces = new bool[totalPiecesInFile];
        }

        // Karşı taraf "Bende şu parça var" mesajı attığında bu metodu çağırıp kaydını güncelleyeceğiz
        public void MarkPieceAsAvailable(int pieceIndex)
        {
            if (pieceIndex >= 0 && pieceIndex < AvailablePieces.Length)
            {
                AvailablePieces[pieceIndex] = true;
                Console.WriteLine($"[BİLGİ] {IpAddress} adlı eşte {pieceIndex}. parça mevcut olarak işaretlendi.");
            }
        }

        public void UpdateBitfield(bool[] bitfield)
        {
            int length = Math.Min(AvailablePieces.Length, bitfield.Length);
            for (int i = 0; i < length; i++)
            {
                AvailablePieces[i] = bitfield[i];
            }
        }

        // Hoca görsün diye şekilli bir yüzde hesaplama metodu
        public double GetCompletionPercentage()
        {
            int haveCount = 0;
            foreach (bool hasPiece in AvailablePieces)
            {
                if (hasPiece) haveCount++;
            }
            return (double)haveCount / AvailablePieces.Length * 100;
        }
    }
}
