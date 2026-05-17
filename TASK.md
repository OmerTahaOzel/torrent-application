# TASK.md - Basit Torrent Uygulamasi

## Mimari
- `torrent application`: Tek WinForms istemci (Seeder + Leecher sekmeleri).
- `Torrent.Core`: Ortak torrent mantigi.
- `Torrent.Tracker`: Merkezi peer discovery servisi.

## Isleyis
1. Node baslatilir (`PeerServer` + `TrackerClient`).
2. Seeder tarafinda dosya 1 MB parcalara bolunur.
3. Parca hash'leri ve Merkle root hesaplanir.
4. `.ttmeta` olusturulur ve tracker'a `REGISTER` atilir.
5. Leecher tarafinda `.ttmeta` yuklenir.
6. `GET_PEERS(infoHash)` ile peer listesi alinir.
7. Handshake/bitfield sonrasi rarest-first ile parcalar istenir.
8. Her parca hash + Merkle proof ile dogrulanip dosyaya yazilir.
9. State `.ttstate` ile tutulur (resume).
10. Tum parcalar tamamlaninca final Merkle root kontrol edilir.

## Tracker API
- `REGISTER(infoHash, peerId, host, port, bitfield)`
- `HEARTBEAT(infoHash, peerId, host, port, bitfield)`
- `UNREGISTER(infoHash, peerId)`
- `GET_PEERS(infoHash)`

## Calistirma
1. Tracker:
   - `dotnet run --project .\Torrent.Tracker\Torrent.Tracker.csproj`
2. WinForms istemci:
   - `dotnet run --project ".\torrent application\torrent application.csproj"`
3. Ayni makinede birden fazla instance icin `Listen Port = 0` kullan.

## Notlar
- TCP only.
- NAT traversal / UPnP yok.
- Tracker bellek-ici calisir.
