# Simple Torrent App (WinForms + TCP + Tracker)

Bu README, projede "hangi ozellik hangi dosyada, hangi satirda" sorusunu dogrudan cevaplamak icin hazirlandi.
Tum referanslar mevcut kod durumuna gore verilmistir.

## 1) Cozum ve Proje Haritasi

- Cozum dosyasi: `TorrentApp.slnx`
  - Projeler: `TorrentApp.slnx:2-4`
- Core kutuphanesi: `Torrent.Core/Torrent.Core.csproj:1-9`
- WinForms istemci: `torrent application/torrent application.csproj:1-17`
  - `RootNamespace`: `torrent application/torrent application.csproj:14`
- Tracker uygulamasi: `Torrent.Tracker/Torrent.Tracker.csproj:1-14`

## 2) Uygulamanin Giris Noktalari

### 2.1 WinForms istemci girisi

- `Program` sinifi: `torrent application/Program.cs:3`
- `Main()` metodu: `torrent application/Program.cs:6-10`
- Form acilisi (`Application.Run(new Form1())`): `torrent application/Program.cs:9`

### 2.2 Tracker girisi

- Console title: `Torrent.Tracker/Program.cs:9`
- Port secimi (`args` veya 7070): `Torrent.Tracker/Program.cs:10`
- `TcpListener` baslatma: `Torrent.Tracker/Program.cs:12-14`
- Cleanup task baslatma: `Torrent.Tracker/Program.cs:19`
- Ana accept dongusu: `Torrent.Tracker/Program.cs:21-25`

## 3) WinForms UI - Hangi Buton Ne Yapiyor

Ana form sinifi: `torrent application/Form1.cs:8`

### 3.1 Form olusumu

- Constructor: `torrent application/Form1.cs:31-35`
- Tum UI'nin kodla kurulmasi: `BuildUi() -> torrent application/Form1.cs:37-95`
- Form basligi (`torrent application`): `torrent application/Form1.cs:39`

### 3.2 Node + Tracker paneli

`BuildUi()` icinde olusur:

- Tracker host textbox: `torrent application/Form1.cs:45`
- Tracker port textbox: `torrent application/Form1.cs:46`
- Advertised host textbox: `torrent application/Form1.cs:47`
- Listen port textbox: `torrent application/Form1.cs:48`
- `Node Baslat` butonu: `torrent application/Form1.cs:49`
- `Auto Port` butonu: `torrent application/Form1.cs:50`
- Bound port label: `torrent application/Form1.cs:51`

Buton davranisi:

- Node baslatma metodu: `StartNodeAsync(bool) -> torrent application/Form1.cs:139-171`
- Indirme varken restart engeli: `torrent application/Form1.cs:141-145`
- Port parse: `torrent application/Form1.cs:154-156`
- Port cakisma fallback sorusu: `torrent application/Form1.cs:157-166`
- Basarili bind bilgisini UI'ya yazma: `torrent application/Form1.cs:168-170`

### 3.3 Seeder sekmesi

UI olusumu:

- Sekme olusturma: `torrent application/Form1.cs:70-72`
- `BuildSeederTab(...)`: `torrent application/Form1.cs:97-113`
- Dosya sec butonu: `torrent application/Form1.cs:101`
- Seeding baslat butonu: `torrent application/Form1.cs:102`
- Meta label: `torrent application/Form1.cs:103`
- Meta'yi leecher alanina aktar butonu: `torrent application/Form1.cs:105`

Davranis:

- Dosya secimi: `PickFile() -> torrent application/Form1.cs:260-267`
- Seeding is akisi: `StartSeedingAsync() -> torrent application/Form1.cs:173-200`
  - Node kontrolu: `torrent application/Form1.cs:175-179`
  - Dosya varlik kontrolu: `torrent application/Form1.cs:181-186`
  - `TorrentNode.StartSeedingAsync(...)` cagri: `torrent application/Form1.cs:190`
  - Meta yolunu UI'ya yazma: `torrent application/Form1.cs:191-193`
- Meta yolunu leecher alanina kopyalama: `CopyMetaToLeecher() -> torrent application/Form1.cs:287-295`

### 3.4 Leecher sekmesi

UI olusumu:

- Sekme olusturma: `torrent application/Form1.cs:73-74`
- `BuildLeecherTab(...)`: `torrent application/Form1.cs:115-137`
- `.ttmeta` sec butonu: `torrent application/Form1.cs:119`
- Hedef klasor sec butonu: `torrent application/Form1.cs:122`
- `Indir` butonu: `torrent application/Form1.cs:124`
- ProgressBar/Label: `torrent application/Form1.cs:126-127`

Davranis:

- Metadata secimi: `PickMeta() -> torrent application/Form1.cs:269-276`
- Cikis klasoru secimi: `PickOutput() -> torrent application/Form1.cs:278-285`
- Download akisi: `StartDownloadAsync() -> torrent application/Form1.cs:202-258`
  - Node kontrolu: `torrent application/Form1.cs:204-208`
  - Ayni anda tek download kontrolu: `torrent application/Form1.cs:210-214`
  - Meta dosya kontrolu: `torrent application/Form1.cs:216-221`
  - Output klasor olusturma: `torrent application/Form1.cs:223-224`
  - Progress callback: `torrent application/Form1.cs:228-234`
  - Core download cagrisi: `torrent application/Form1.cs:237`
  - Cancel yakalama: `torrent application/Form1.cs:243-246`

### 3.5 Log ve kapanis davranisi

- UI-safe log yazimi: `Log(string) -> torrent application/Form1.cs:297-306`
- Form kapanisinda download iptal mekanizmasi: `OnFormClosing(...) -> torrent application/Form1.cs:321-333`
- Node dispose: `OnFormClosed(...) -> torrent application/Form1.cs:335-343`
- Cancel sonrasi formu tekrar kapatma: `CloseAfterCancellationAsync() -> torrent application/Form1.cs:345-363`

### 3.6 UI yardimci metotlar

- Label factory: `L(...) -> torrent application/Form1.cs:308`
- TextBox factory: `Tb(...) -> torrent application/Form1.cs:309`
- Button factory: `Btn(...) -> torrent application/Form1.cs:311-316`
- Port parse helper: `ParsePort(...) -> torrent application/Form1.cs:318-319`

### 3.7 Designer dosyasi

- Minimal init: `torrent application/Form1.Designer.cs:16-22`
- `Text = "Form1"` degeri kod tarafinda override edilir (`BuildUi`):
  - Designer: `torrent application/Form1.Designer.cs:21`
  - Runtime override: `torrent application/Form1.cs:39`

## 4) Core Mimari - Sinif Bazli Detay

## 4.1 TorrentNode (ana orkestrator)

Dosya: `Torrent.Core/Runtime/TorrentNode.cs`

- Sinif tanimi: `:17`
- Runtime alanlari:
  - Seed cache: `:19`
  - Tracker ve peer server referanslari: `:21-23`
  - Announce listesi: `:24`
- Node baslatma: `Start(...) -> :35-51`
  - Tracker client olusturma: `:37`
  - PeerServer start: `:40-42`
  - Heartbeat loop baslatma: `:47-48`
- Seeding baslatma: `StartSeedingAsync(...) -> :53-80`
  - Metadata + parcalar + Merkle tree uretimi: `:56`
  - `.ttmeta` kaydetme: `:57`
  - `_seeded` kaydi: `:66`
  - Tracker register: `:70-76`
- Download: `DownloadAsync(...) -> :82-152`
  - Metadata/state yukleme: `:92-95`
  - Tracker'dan peer listesi: `:97-100`
  - Peer yoksa hata: `:102-105`
  - Session baglantilari: `:110-123`
  - Scheduler + file store: `:130-131`
  - Worker tasklari: `:134-135`
  - Tum parca bitti kontrolu: `:137-140`
  - Final hash dogrulamasi: `:142`
- Peer worker: `RunPeerWorkerAsync(...) -> :154-192`
  - Parca tahsisi: `:166-170`
  - Parca istegi: `:172`
  - Hata durumunda tekrar kuyruk: `:173-183`
  - Dogrulama sonrasi yazma + state save: `:185-188`
- Parca hash + Merkle proof dogrulamasi: `VerifyPiece(...) -> :207-216`
- Final Merkle root dogrulamasi: `VerifyFinalHash(...) -> :218-246`
- Heartbeat dongusu: `HeartbeatLoopAsync(...) -> :254-279`
  - 10 saniyede bir heartbeat: `:262-263` ve `:272`
- Dispose/unregister: `DisposeAsync() -> :289-314`
  - Tum announced torrentler icin `UNREGISTER`: `:295-300`

## 4.2 PeerServer (seeder tarafi server)

Dosya: `Torrent.Core/Runtime/PeerServer.cs`

- Sinif: `:13`
- Start + bind: `Start(...) -> :28-55`
- Accept loop: `AcceptLoopAsync(...) -> :57-83`
- Client handler: `HandleClientAsync(...) -> :85-200`
  - Outbound queue (`Channel`): `:90`
  - Mesaj okuma: `:116`
  - Handshake -> Bitfield response: `:124-143`
  - Metadata request -> response: `:146-155`
  - Piece request -> PieceData + MerkleProof: `:158-177`
- Dispose: `DisposeAsync() -> :202-208`

## 4.3 PeerSession (leecher tarafi client)

Dosya: `Torrent.Core/Runtime/PeerSession.cs`

- Sinif: `:12`
- Connect + handshake + bitfield bekleme: `ConnectAsync(...) -> :34-53`
- Outbound gonderim: `SendAsync(...) -> :55-58`
- Parca isteme ve timeout: `RequestPieceAsync(...) -> :60-87`
- Send loop: `SendLoopAsync(...) -> :89-109`
- Receive loop: `ReceiveLoopAsync(...) -> :111-146`
  - Bitfield yakalama: `:128-132`
  - PieceData waiter esleme: `:133-139`
- Dispose: `DisposeAsync() -> :148-166`

## 4.4 Merkle ve metadata

Dosya: `Torrent.Core/Runtime/MerkleAndMetadata.cs`

### MerkleTree

- Sinif: `:12`
- Agac kurma: `BuildFromLeafHashes(...) -> :24-50`
- Proof uretme: `GetProofHex(...) -> :52-75`
- Proof dogrulama: `VerifyPiece(...) -> :77-89`
- Parent hash hesaplama: `HashConcat(...) -> :91-97`

### TorrentMetadataBuilder

- Sinif: `:100`
- Varsayilan parca boyutu (1 MB): `:102`
- Dosyadan metadata/parca/hash uretme: `BuildFromFile(...) -> :104-152`
- InfoHash hesaplama (canonical json + SHA-256): `BuildInfoHash(...) -> :154-169`

## 4.5 SeededTorrent modeli

Dosya: `Torrent.Core/Runtime/SeededTorrent.cs`

- Sinif: `:5`
- `Metadata`, `Pieces`, `Tree` alanlari: `:7-9`
- Seeder bitfield'i (tamami true): `BuildSeederBitfield() -> :11-20`

## 4.6 Scheduler

Dosya: `Torrent.Core/Download/PieceScheduler.cs`

- Sinif: `:7`
- Constructor + rarest-first sirasi: `:14-18`
- Peer icin parca secme: `TryAcquirePieceForPeer(...) -> :20-44`
- Tamamlandi isaretleme: `MarkCompleted(...) -> :46-53`
- Basarisiz parca geri alma: `MarkFailed(...) -> :55-61`
- Tum parcalar tamamlandi mi: `IsCompleted() -> :63-69`
- Rarest-first hesap: `BuildRarestFirstOrder(...) -> :71-89`

## 4.7 IO katmani

### MetadataStore

Dosya: `Torrent.Core/IO/MetadataStore.cs`

- `.ttmeta` yazma: `Save(...) -> :11-17`
- `.ttmeta` okuma: `Load(...) -> :19-29`

### StateStore

Dosya: `Torrent.Core/IO/StateStore.cs`

- State dosya yolu: `GetStatePath(...) -> :12-15`
- State yukle veya olustur: `LoadOrCreate(...) -> :17-53`
  - Yoksa sifirdan state: `:20-29`
  - InfoHash farkliysa reset: `:33-42`
  - Parca sayisi degismisse resize: `:44-49`
- State kaydetme: `Save(...) -> :55-60`

### PieceFileStore

Dosya: `Torrent.Core/IO/PieceFileStore.cs`

- Constructor + dosya boyutunu metadata'ya sabitleme: `:11-20`
- Parca okuma: `ReadPiece(...) -> :22-47`
- Parca yazma + flush: `WritePiece(...) -> :49-55`

## 4.8 Tracker istemcisi

Dosya: `Torrent.Core/Tracker/TrackerClient.cs`

- `RegisterAsync`: `:22-32`
- `HeartbeatAsync`: `:33-43`
- `UnregisterAsync`: `:44-51`
- `GetPeersAsync`: `:52-67`
- Ortak request/response iletimi: `SendAsync(...) -> :78-92`

## 4.9 Protokol ve modeller

### Mesaj tipleri

Dosya: `Torrent.Core/Protocol/Messages.cs`

- `PeerMessageType` enum: `:7-18`
- `PeerMessage`: `:20-31`
- `TrackerRequest`: `:33-41`
- `TrackerResponse`: `:43-48`

### Wire katmani

Dosya: `Torrent.Core/Protocol/WireIO.cs`

- `WriteAsync` (length prefix + json payload): `:14-20`
- `ReadAsync` (length kontrol + deserialize): `:22-45`
- Paket boyutu limiti (<= 256MB): `:31-35`
- `ReadExactAsync`: `:47-62`

### Runtime modelleri

Dosya: `Torrent.Core/Models/RuntimeModels.cs`

- `TorrentState`: `:5-11`
- `DownloadProgress`: `:13-21`
- `PortAllocationResult`: `:23-30`
- `PeerEndpoint`: `:32-38`

### Metadata modeli

Dosya: `Torrent.Core/Models/TorrentMetadata.cs`

- Alanlar: `:5-14`

## 5) Tracker Servisi - Komut Davranislari

Dosya: `Torrent.Tracker/Program.cs`

- Komut dispatch switch:
  - `REGISTER`: `:46`
  - `HEARTBEAT`: `:47` (Register ile ayni akisa gider)
  - `UNREGISTER`: `:48`
  - `GET_PEERS`: `:49`
- Register implementasyonu: `Register(...) -> :62-82`
- Unregister implementasyonu: `Unregister(...) -> :84-94`
- Peer filtreleme + stale dislama: `GetPeers(...) -> :96-118`
  - 2 dk threshold: `:105`
- Periyodik stale cleanup: `CleanupLoopAsync(...) -> :120-145`
  - 30 sn delay: `:143`
- Tracker'daki peer kayit modeli: `TrackedPeer -> :147-154`

## 6) Uctan Uca Akis - Kod Referansli

### 6.1 Node baslatma

1. UI butonu `Node Baslat`: `torrent application/Form1.cs:49`
2. `StartNodeAsync(false)`: `torrent application/Form1.cs:139-171`
3. Core `TorrentNode.Start(...)`: `Torrent.Core/Runtime/TorrentNode.cs:35-51`
4. Server bind: `Torrent.Core/Runtime/PeerServer.cs:28-44`
5. Tracker baglantisi: `Torrent.Core/Runtime/TorrentNode.cs:37`

### 6.2 Seeding

1. UI butonu `Seeding Baslat`: `torrent application/Form1.cs:102`
2. `StartSeedingAsync()` (UI): `torrent application/Form1.cs:173-200`
3. Core `StartSeedingAsync(...)`: `Torrent.Core/Runtime/TorrentNode.cs:53-80`
4. Metadata olusturma: `Torrent.Core/Runtime/MerkleAndMetadata.cs:104-152`
5. `.ttmeta` kayit: `Torrent.Core/IO/MetadataStore.cs:11-17`
6. Tracker `REGISTER`: `Torrent.Core/Runtime/TorrentNode.cs:70-76`

### 6.3 Download

1. UI butonu `Indir`: `torrent application/Form1.cs:124`
2. `StartDownloadAsync()` (UI): `torrent application/Form1.cs:202-258`
3. Core `DownloadAsync(...)`: `Torrent.Core/Runtime/TorrentNode.cs:82-152`
4. Tracker `GET_PEERS`: `Torrent.Core/Runtime/TorrentNode.cs:97`
5. Peer connect + handshake: `Torrent.Core/Runtime/TorrentNode.cs:110-117` ve `Torrent.Core/Runtime/PeerSession.cs:34-53`
6. Scheduler rarest-first: `Torrent.Core/Download/PieceScheduler.cs:14-18` ve `:71-89`
7. Parca indirme worker: `Torrent.Core/Runtime/TorrentNode.cs:154-192`
8. Hash+Merkle dogrulama: `Torrent.Core/Runtime/TorrentNode.cs:207-216`
9. Parca yazma: `Torrent.Core/IO/PieceFileStore.cs:49-55`
10. State kaydetme: `Torrent.Core/IO/StateStore.cs:55-60`
11. Final root kontrolu: `Torrent.Core/Runtime/TorrentNode.cs:218-246`

## 7) Dosya Formatlari - Kod Kaynagi

### 7.1 `.ttmeta`

- Model tanimi: `Torrent.Core/Models/TorrentMetadata.cs:5-14`
- Yazma: `Torrent.Core/IO/MetadataStore.cs:11-17`
- Okuma: `Torrent.Core/IO/MetadataStore.cs:19-29`
- Uretim: `Torrent.Core/Runtime/MerkleAndMetadata.cs:140-151`

### 7.2 `.ttstate`

- Model tanimi: `Torrent.Core/Models/RuntimeModels.cs:5-11`
- Path kurali (`targetFilePath + .ttstate`): `Torrent.Core/IO/StateStore.cs:12-15`
- Yukleme/olusturma: `Torrent.Core/IO/StateStore.cs:17-53`
- Kaydetme: `Torrent.Core/IO/StateStore.cs:55-60`

## 8) Heartbeat ve Stale Peer Mantigi (Koddan)

- Node heartbeat dongusu: `Torrent.Core/Runtime/TorrentNode.cs:254-279`
- Heartbeat tracker cagrisi: `Torrent.Core/Runtime/TorrentNode.cs:262-263`
- Tracker `HEARTBEAT` komutu `Register` akisina maplenir: `Torrent.Tracker/Program.cs:47`
- Tracker stale filtresi (`GET_PEERS`): `Torrent.Tracker/Program.cs:105-108`
- Tracker stale cleanup dongusu: `Torrent.Tracker/Program.cs:120-145`

## 9) Derleme ve Calistirma

### 9.1 Build

```powershell
dotnet build .\TorrentApp.slnx
```

### 9.2 Tracker

```powershell
dotnet run --project .\Torrent.Tracker\Torrent.Tracker.csproj
```

### 9.3 WinForms istemci

```powershell
dotnet run --project ".\torrent application\torrent application.csproj"
```

## 10) Hata Mesajlari ve Kod Kaynagi

- `Tracker uzerinde uygun peer bulunamadi.`
  - Kaynak: `Torrent.Core/Runtime/TorrentNode.cs:104`
- `Peer baglantisi kurulamadigi icin indirme baslayamadi.`
  - Kaynak: `Torrent.Core/Runtime/TorrentNode.cs:127`
- `Final dosya Merkle root dogrulamasindan gecmedi.`
  - Kaynak: `Torrent.Core/Runtime/TorrentNode.cs:244`
- `Port kullanimda`
  - Kaynak: `Torrent.Core/Runtime/PeerServer.cs:52`

## 11) Kisa Ozet

- UI tarafindaki tum aksiyonlar `Form1.cs` uzerinden `TorrentNode` cagrilarina bagli.
- Core tarafta tum download/seeding orkestrasyonu `TorrentNode` uzerinden donuyor.
- Tracker tamamen `Program.cs` tek dosyada ve in-memory tabloyla calisiyor.
- README'deki her madde dogrudan ilgili satir referansi ile verildi.
