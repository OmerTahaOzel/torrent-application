using System;
using System.IO;
using System.Threading.Tasks;
using Torrent.Core;
using Torrent.Network;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "P2P Torrent Motoru - Ağ Üzerinden Canlı Test";
        Console.WriteLine("=== BÜYÜK YARRAKLI KUMANDANIMIN P2P AĞI ===\n"); // Hocaya göstermeden önce burayı silersin amk :D

        // 1 parçalık motorumuzu hazırlıyoruz
        TorrentEngine engine = new TorrentEngine(totalPieces: 1);

        Console.WriteLine("Lütfen bir rol seçin kumandanım:");
        Console.WriteLine("1 - Mekanı Aç ve Bekle (Dosyayı alacak kişi)");
        Console.WriteLine("2 - Arkadaşına Bağlan ve Dosyayı Fırlat (Gönderen kişi)");
        Console.Write("\nSeçiminiz (1 veya 2): ");

        string secim = Console.ReadLine();

        if (secim == "1")
        {
            // === ALICI MODU (SERVER) ===
            TcpServer server = new TcpServer(engine);
            server.Start(8080);

            Console.WriteLine("\n[SİSTEM] Mekan açıldı! Arkadaşına IP adresini ver ve sana dosya atmasını bekle...");
            Console.WriteLine("[SİSTEM] Dosya geldiğinde 'Karabuk_P2P_Indirilen.txt' adıyla klasöre düşecek.");
            Console.WriteLine("Çıkmak için 'Enter' tuşuna basın.");
            Console.ReadLine();

            server.Stop();
        }
        else if (secim == "2")
        {
            // === GÖNDERİCİ MODU (CLIENT) ===
            Console.Write("\nBağlanılacak Arkadaşının IP Adresi Nedir? (Örn: 192.168.1.15): ");
            string hedefIp = Console.ReadLine();

            // Gönderilecek test dosyasını yaratıyoruz
            string sourceFile = "kaynak_dosyamiz.txt";
            File.WriteAllText(sourceFile, "Büyük kumandan Ömer'in Karabük P2P ağı üzerinden yolladığı efsanevi dosyadır. Hedefe başarıyla ulaştı!");
            Console.WriteLine($"\n[SİSTEM] Gönderilecek dosya hazırlandı: {sourceFile}");

            PeerClient client = new PeerClient();

            Console.WriteLine($"[SİSTEM] {hedefIp} adresine taarruz başlatılıyor...");
            await client.ConnectToPeerAsync(hedefIp, 8080);

            await Task.Delay(500); // Tokalaşma için ufak mola

            Console.WriteLine("\n[SİSTEM] Dosya bombası fırlatılıyor!");
            await client.SendFilePieceAsync(sourceFile, pieceIndex: 0);

            Console.WriteLine("\n[SİSTEM] Dosya başarıyla yollandı! Arkadaşın klasörünü kontrol etsin.");
            Console.WriteLine("Çıkmak için 'Enter' tuşuna basın.");
            Console.ReadLine();

            client.Disconnect();
        }
        else
        {
            Console.WriteLine("Yanlış tuşa bastın amk, baştan aç programı.");
        }
    }
}