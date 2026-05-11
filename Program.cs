using System;
using System.IO;
using System.Threading.Tasks;
using Torrent.Core;
using Torrent.Network;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "P2P Torrent Motoru - Merkle Tree";
        Console.WriteLine("=== P2P Torrent Demo (Merkle Doğrulama) ===\n");

        TorrentEngine engine = new TorrentEngine(totalPieces: 1);

        Console.WriteLine("Lütfen bir rol seçin:");
        Console.WriteLine("1 - Alıcı (Sunucu)");
        Console.WriteLine("2 - Gönderici (İstemci)");
        Console.Write("\nSeçiminiz (1 veya 2): ");

        string? secim = Console.ReadLine();

        if (secim == "1")
        {
            TcpServer server = new TcpServer(engine);
            server.Start(8080);

            Console.WriteLine("\n[SİSTEM] Sunucu açıldı. IP adresini paylaş ve dosya bekle.");
            Console.WriteLine("[SİSTEM] Doğrulanan parçalar 'Karabuk_P2P_Indirilen.txt' dosyasına yazılacak.");
            Console.WriteLine("Çıkmak için 'Enter' tuşuna basın.");
            Console.ReadLine();

            server.Stop();
        }
        else if (secim == "2")
        {
            Console.Write("\nBağlanılacak alıcının IP adresi nedir? (Örn: 192.168.1.15): ");
            string? hedefIp = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(hedefIp))
            {
                Console.WriteLine("[HATA] IP adresi boş olamaz.");
                return;
            }

            string sourceFile = "kaynak_dosyamiz.txt";
            File.WriteAllText(sourceFile, "Bu dosya P2P ağında Merkle tree doğrulaması ile gönderildi.");
            Console.WriteLine($"\n[SİSTEM] Gönderilecek dosya hazırlandı: {sourceFile}");

            PeerClient client = new PeerClient();
            Console.WriteLine($"[SİSTEM] {hedefIp} adresine bağlanılıyor...");
            await client.ConnectToPeerAsync(hedefIp, 8080, sourceFile);

            Console.WriteLine("\n[SİSTEM] İstek geldikçe parçalar Merkle proof ile otomatik gönderilecek.");
            Console.WriteLine("Çıkmak için 'Enter' tuşuna basın.");
            Console.ReadLine();

            client.Disconnect();
        }
        else
        {
            Console.WriteLine("Geçersiz seçim. Programı yeniden başlatın.");
        }
    }
}
