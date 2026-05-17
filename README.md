# Simple Torrent App (WinForms + TCP + Tracker)

Bu dokuman iki amaca hizmet eder:
1) Projeyi teknik olarak aciklar (ne ne ise yarar)
2) Her iddianin kod karsiligini satir referansi ile verir

## 1) Cozum ve Proje Haritasi

- `TorrentApp.slnx` cozum dosyasi, tum aktif projelerin birlikte derlenmesini saglar.
  Referans: `TorrentApp.slnx:1-5`
- `Torrent.Core`, torrent mantiginin tamamini tasiyan kutuphanedir. UI veya tracker'dan bagimsiz is kurallari buradadir.
  Referans: `Torrent.Core/Torrent.Core.csproj:1-9`
- `torrent application`, kullanicinin dogrudan kullandigi WinForms istemcisidir (seeding + downloading tek uygulamada).
  Referans: `torrent application/torrent application.csproj:1-17`
- `Torrent.Tracker`, peer listesini merkezi olarak tutan ve dagitan hafif servis uygulamasidir.
  Referans: `Torrent.Tracker/Torrent.Tracker.csproj:1-14`

## 2) Uygulamanin Giris Noktalari

### 2.1 WinForms istemci girisi

- `Program.Main()`, masaustu uygulamasini baslatir ve ana formu acar.
  Referanslar: `torrent application/Program.cs:3-10`, ozellikle `:9`

Ne ise yarar:
- Tum UI akisini baslatan tek giris burasidir.
- Kodun geri kalani bu form uzerinden tetiklenir.

### 2.2 Tracker girisi

- Tracker process'i acilir, TCP listener baslatir, baglanti kabul eder ve her client'i ayri task'ta isler.
  Referanslar: `Torrent.Tracker/Program.cs:9-25`

Ne ise yarar:
- Seeder/leecher birbirini dogrudan bilmez; once tracker'a kaydolur ve tracker'dan peer listesi alir.
- Bu yuzden tracker proje akisinin merkezidir ama veri depolama olarak in-memory calisir.

## 3) WinForms UI (torrent application)

Ana form: `torrent application/Form1.cs:8`

### 3.1 Form olusumu

- Constructor ve `BuildUi()` tum kontrolleri runtime'da olusturur.
  Referanslar: `torrent application/Form1.cs:31-35`, `:37-95`
- Form basligi runtime'da `torrent application` olarak set edilir.
  Referans: `torrent application/Form1.cs:39`

Ne ise yarar:
- Designer'a bagimli olmayan, kodla kontrol edilen dinamik bir UI kurar.
- UI davranislarini tek dosyada toplu izlemeyi kolaylastirir.

### 3.2 Node + Tracker paneli

- Host/port alanlari ve `Node Baslat` / `Auto Port` butonlari bu bolumde tanimlidir.
  Referanslar: `torrent application/Form1.cs:44-60`
- Baslatma is akisi `StartNodeAsync` icindedir.
  Referans: `torrent application/Form1.cs:139-171`

Ne ise yarar:
- Peer server'i hangi portta acacagini belirler.
- Tracker endpoint'i ve advertise edilen host degerini belirler.
- Port cakismasinda fallback (`Auto Port`) ile kullaniciyi bloklamaz.

### 3.3 Seeder sekmesi

- Dosya secimi, seeding baslatma ve metadata gosterme kontrolleri.
  Referanslar: `torrent application/Form1.cs:97-113`
- Seeding akisi `StartSeedingAsync`.
  Referans: `torrent application/Form1.cs:173-200`

Ne ise yarar:
- Lokal dosyayi parcalayip torrent metadata'si ureterek paylasimi baslatir.
- Tracker'a kayit gondererek diger peer'lerin seni bulmasini saglar.

### 3.4 Leecher sekmesi

- `.ttmeta` secimi, hedef klasor secimi ve indirme baslatma kontrolleri.
  Referanslar: `torrent application/Form1.cs:115-137`
- Download akisi `StartDownloadAsync`.
  Referans: `torrent application/Form1.cs:202-258`

Ne ise yarar:
- Metadata'ya gore dogru torrent'i bulur.
- Tracker'dan peer alir ve parcalari dogrulayarak hedef dosyayi olusturur.

### 3.5 Log, kapanis ve iptal davranisi

- Thread-safe log yazimi: `Log(...)`.
  Referans: `torrent application/Form1.cs:297-306`
- Form kapanirken aktif download varsa iptal edip task'i bekler.
  Referanslar: `torrent application/Form1.cs:321-333`, `:345-363`

Ne ise yarar:
- Ani kapanista bozuk state veya yarim kalmis operasyon riskini azaltir.
- Kullaniciya nerede hata oldugunu log uzerinden gosterebilir.

## 4) Core Katmani (Torrent.Core)

### 4.1 TorrentNode - ana orkestrator

Dosya: `Torrent.Core/Runtime/TorrentNode.cs`

Ne yapar:
- Node lifecycle yonetimi (start, heartbeat, dispose)
- Seeding kaydi ve tracker register
- Download orchestration (peer bul, session ac, scheduler calistir, verify, save)

Kritik noktalar:
- `Start(...)`: `:35-51`
- `StartSeedingAsync(...)`: `:53-80`
- `DownloadAsync(...)`: `:82-152`
- Worker loop: `:154-192`
- Piece verify: `:207-216`
- Final file verify: `:218-246`
- Heartbeat loop: `:254-279`
- Unregister/dispose: `:289-314`

Neden onemli:
- Uygulamanin asil "torrent motoru" burasi.
- UI sadece bu sinifi cagirir; is kurallari daginmasin diye merkezde toplanmistir.

### 4.2 PeerServer - seeder tarafi sunucu

Dosya: `Torrent.Core/Runtime/PeerServer.cs`

Ne yapar:
- TCP baglanti kabul eder.
- Handshake/bitfield/metadata/piece isteklerini cevaplar.
- Merkle proof ile parca datasi doner.

Referanslar:
- Start/bind: `:28-55`
- Accept loop: `:57-83`
- Client handler: `:85-200`
- `RequestPiece -> PieceData`: `:158-177`

Neden onemli:
- Leecher parcalari bu katmandan ceker.
- Bu kod olmazsa uygulama sadece metadata uretebilir ama peer'a veri veremez.

### 4.3 PeerSession - leecher tarafi istemci

Dosya: `Torrent.Core/Runtime/PeerSession.cs`

Ne yapar:
- Peer'e baglanir, handshake atar, bitfield alir.
- Piece request gonderir, timeout ile cevap bekler.
- Receive loop'ta piece cevaplarini dogru bekleyen task'a map eder.

Referanslar:
- Connect/handshake: `:34-53`
- RequestPiece: `:60-87`
- Receive loop: `:111-146`

Neden onemli:
- Parallel download'in temelinde birden fazla `PeerSession` vardir.
- Session seviyesi timeout/geri donus yönetimi download dayanıklılığını belirler.

### 4.4 Merkle + Metadata builder

Dosya: `Torrent.Core/Runtime/MerkleAndMetadata.cs`

Ne yapar:
- Dosyayi parcalara ayirir.
- Her parcayi SHA-256 hashler.
- Hashlerden Merkle tree uretir.
- Parca proof uretir ve verify eder.
- Deterministik `InfoHash` hesaplar.

Referanslar:
- `MerkleTree`: `:12-98`
- `BuildFromFile(...)`: `:104-152`
- `BuildInfoHash(...)`: `:154-169`

Neden onemli:
- Dosya butunluk garantisi buradan gelir.
- Salt byte aldim yazdim degil, kriptografik dogrulama yapilir.

### 4.5 Scheduler

Dosya: `Torrent.Core/Download/PieceScheduler.cs`

Ne yapar:
- Rarest-first parca sirasi cikarir.
- Ayni parcayi iki peer'in ayni anda almasini `_inFlight` ile engeller.
- Basarisiz parcayi tekrar aday yapar.

Referanslar:
- Constructor/rarity: `:14-18`, `:71-89`
- Acquire/complete/fail: `:20-61`

Neden onemli:
- Parallel indirmede verim ve dogruluk scheduler kalitesiyle dogrudan ilgilidir.

### 4.6 IO katmani

- `.ttmeta` oku/yaz: `Torrent.Core/IO/MetadataStore.cs:11-29`
- `.ttstate` resume state oku/yaz: `Torrent.Core/IO/StateStore.cs:12-60`
- Piece bazli random dosya erisimi: `Torrent.Core/IO/PieceFileStore.cs:11-60`

Ne ise yarar:
- Uygulamayi "indirirken kapaninca sifirdan baslama" durumundan kurtarir.
- Dogru offset'e dogru parcayi yazarak final dosyayi reconstruct eder.

### 4.7 TrackerClient

Dosya: `Torrent.Core/Tracker/TrackerClient.cs`

Ne yapar:
- `REGISTER`, `HEARTBEAT`, `UNREGISTER`, `GET_PEERS` komutlarini yollar.

Referanslar:
- API metotlari: `:22-67`
- Ortak send/read: `:78-92`

Neden onemli:
- Peer discovery zincirinin istemci tarafindaki tek baglanti noktasi.

### 4.8 Protokol modelleri ve wire format

- Mesaj tipleri: `Torrent.Core/Protocol/Messages.cs:7-48`
- Length-prefixed JSON I/O: `Torrent.Core/Protocol/WireIO.cs:14-62`

Ne ise yarar:
- Tüm ag haberlesmesi ortak formatla ilerler.
- Paket boyutu limiti (`<= 256MB`) kaba hatalara karsi koruma saglar.

## 5) Tracker Servisi Ayrintisi

Dosya: `Torrent.Tracker/Program.cs`

Ne yapar:
- InfoHash bazli peer tablosu tutar.
- Register/heartbeat ile `LastSeenUtc` gunceller.
- `GET_PEERS` cevabinda stale veya kendini isteyen peer'i filtreler.
- Ayrica periyodik cleanup dongusuyle stale kayitlari temizler.

Referanslar:
- Switch dispatch: `:44-51`
- Register: `:62-82`
- Unregister: `:84-94`
- GetPeers filtreleri: `:96-118`
- Cleanup loop: `:120-145`

Neden onemli:
- Aktif peer listesinin "gercege yakin" kalmasini saglar.
- Ozellikle demo/testte bozuk endpoint gorunmesini azaltir.

## 6) Uctan Uca Senaryo (Adim Adim)

1. Kullanici node'u baslatir.
   Referans: `torrent application/Form1.cs:49`, `:139-171`
2. Seeder dosya secer ve seeding baslatir.
   Referans: `torrent application/Form1.cs:101-103`, `:173-200`
3. Core metadata + merkle uretir ve tracker'a register eder.
   Referans: `Torrent.Core/Runtime/TorrentNode.cs:56-76`
4. Leecher `.ttmeta` secer ve indir der.
   Referans: `torrent application/Form1.cs:119`, `:124`, `:202-258`
5. Core tracker'dan peer alir, session acip parcalari indirir.
   Referans: `Torrent.Core/Runtime/TorrentNode.cs:97-135`
6. Her parca hash + merkle proof ile dogrulanir, dosyaya yazilir, state kaydolur.
   Referans: `Torrent.Core/Runtime/TorrentNode.cs:172-190`, `:207-216`
7. Tum parcalar bitince final merkle root dogrulamasi yapilir.
   Referans: `Torrent.Core/Runtime/TorrentNode.cs:218-246`

## 7) Dosya Formatlari

### 7.1 `.ttmeta`

Ne ise yarar:
- Dosyanin torrent kimligini ve parca hashlerini tasir.
- Leecher dosyayi nereden nasil dogrulayacagini bu dosyadan ogrenir.

Kod referanslari:
- Model: `Torrent.Core/Models/TorrentMetadata.cs:5-14`
- Yazma: `Torrent.Core/IO/MetadataStore.cs:11-17`
- Okuma: `Torrent.Core/IO/MetadataStore.cs:19-29`

### 7.2 `.ttstate`

Ne ise yarar:
- Hangi parcalar tamamlandi bilgisini tutar.
- Uygulama kapanip acildiginda kaldigi yerden devam etmeyi saglar.

Kod referanslari:
- Model: `Torrent.Core/Models/RuntimeModels.cs:5-11`
- Yukleme/olusturma: `Torrent.Core/IO/StateStore.cs:17-53`
- Kaydetme: `Torrent.Core/IO/StateStore.cs:55-60`

## 8) Hata Mesajlari ve Anlami

- `Tracker uzerinde uygun peer bulunamadi.`
  - Kaynak: `Torrent.Core/Runtime/TorrentNode.cs:104`
  - Anlam: Tracker bu infoHash icin uygun peer dondurmedi.

- `Peer baglantisi kurulamadigi icin indirme baslayamadi.`
  - Kaynak: `Torrent.Core/Runtime/TorrentNode.cs:127`
  - Anlam: Peer listesi var ama hicbiriyle session acilamadi.

- `Final dosya Merkle root dogrulamasindan gecmedi.`
  - Kaynak: `Torrent.Core/Runtime/TorrentNode.cs:244`
  - Anlam: Indirilen dosyanin butunluk kontrolu basarisiz.

- `Port kullanimda`
  - Kaynak: `Torrent.Core/Runtime/PeerServer.cs:52`
  - Anlam: Istenen listen portu baska bir process tarafindan kullaniliyor.

## 9) Derleme ve Calistirma

```powershell
dotnet build .\TorrentApp.slnx
dotnet run --project .\Torrent.Tracker\Torrent.Tracker.csproj
dotnet run --project ".\torrent application\torrent application.csproj"
```

## 10) Kisa Teknik Ozet

- UI katmani sadece tetikler, is kurali `Torrent.Core` icindedir.
- Tracker sadece peer discovery yapar, dosya tasimaz.
- Dosya butunlugu parca hash + Merkle root ile iki asamali dogrulanir.
- Resume mantigi `.ttstate` ile saglanir.
