using System;
using System.Collections.Generic;
using Torrent.Models;

namespace Torrent.Core
{
    public class TorrentEngine
    {
        // Mekana bağlanan tüm elemanların (eşlerin) listesi
        private readonly List<Peer> _connectedPeers;

        // Bizim (kendi bilgisayarımızın) elindeki parçaların haritası
        private readonly bool[] _myBitfield;
        private readonly int _totalPieces;

        // Patron doğduğunda kaç parçalık bir dosya indireceğimizi bilmeli
        public TorrentEngine(int totalPieces)
        {
            _totalPieces = totalPieces;
            _myBitfield = new bool[totalPieces];
            _connectedPeers = new List<Peer>();
        }

        // 1. GÖREV: Mekana yeni giren adamı sicile (listeye) kaydet
        public void AddPeer(string ipAddress, int port)
        {
            Peer newPeer = new Peer(ipAddress, port, _totalPieces);
            _connectedPeers.Add(newPeer);
            Console.WriteLine($"[MOTOR] {ipAddress}:{port} ağa eklendi. Toplam Eş Sayısı: {_connectedPeers.Count}");
        }

        // 2. GÖREV: Kuyruktan çıkan mesajları yorumla ve ne yapılacağına karar ver
        public void ProcessMessage(P2PMessage message, string senderIp)
        {
            switch (message.Type)
            {
                case MessageType.Handshake:
                    Console.WriteLine($"[MOTOR] {senderIp} ile el sıkışıldı. Bitfield (parça listesi) bekleniyor...");
                    // TODO: Biz de adama kendi Bitfield'ımızı yollayacağız
                    break;

                case MessageType.Bitfield:
                    Console.WriteLine($"[MOTOR] {senderIp} adlı eşten parça haritası geldi.");
                    // TODO: Gelen byte array'i okuyup adamın profiline işleyeceğiz
                    break;

                case MessageType.RequestPiece:
                    Console.WriteLine($"[MOTOR] {senderIp} bizden {message.PieceIndex}. parçayı istiyor.");
                    // TODO: FileManager ile dosyayı okuyup, SendPiece mesajıyla adama fırlatacağız
                    break;

                case MessageType.SendPiece:
                    Console.WriteLine($"[MOTOR] {senderIp} bize {message.PieceIndex}. parçayı yolladı. Diske yazılıyor...");

                    // Gelen dosyayı kaydedeceğimiz isim
                    string targetFileName = "Karabuk_P2P_Indirilen.txt";

                    // Amele sınıfı çağırıp diske yazdırıyoruz
                    Torrent.IO.FileManager fm = new Torrent.IO.FileManager();
                    fm.WritePiece(targetFileName, message.PieceIndex, message.Payload);

                    // Kendi haritamızda bu parçayı "Bende var" (true) olarak işaretliyoruz
                    _myBitfield[message.PieceIndex] = true;

                    Console.WriteLine($"[MOTOR] Dosya transferi başarıyla tamamlandı ve diske kaydedildi!");
                    break;

                default:
                    Console.WriteLine($"[MOTOR] Ne idüğü belirsiz bir mesaj geldi amk... Pardon, bilinmeyen mesaj tipi: {message.Type}");
                    break;
            }
        }
    }
}