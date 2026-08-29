# LINK Logistics — Proof of Delivery — Proje Durumu

_Son güncelleme: 2026-08-29_

## ⚡ Proje VPS'e taşındı (2026-08-29)

Artık geliştirme + deploy **Cloud VPS** üzerinde: `ssh hub-vps` → `cd /srv/linklogistics`.
- Repo git bundle ile taşındı (main @ `2a23cb2`), GitHub remote yok.
- `.env` sunucuda güçlü secret'larla yeniden üretildi (dev değerleri değil).
- `docker compose up -d` çalışıyor. Public: api `185.187.169.151:8080`, ops panel `:8081`,
  MinIO API `:9100`. sqlserver `1433` + MinIO console `9101` sadece localhost (SSH tünel gerekir).
- `docker-compose.override.yml` = VPS binding'leri (lokal dev'de kullanılmaz).
- Redeploy: `cd /srv/linklogistics && git pull && docker compose up -d --build`
  sonra `sudo ufw-docker allow linklogistics-api-1 8080` (+ web 8080, minio 9000) tekrar çalıştır.
- Claude Code sunucuda kurulu (`claude`).
- Detay: `~/.claude/.../memory/hub-vps.md`.

Windows'taki lokal Docker stack hâlâ ayakta olabilir — artık gerekmiyor,
`cd logistic && docker compose down` ile kapatılabilir.

---

Bu dosya "nerede kaldık, servisler nasıl çalışıyor" özetidir. Detaylı build
geçmişi için `~/.claude/.../memory/link-logistics-build-progress.md` ve cihaz
testi notları için `link-logistics-maui-testing.md`.

---

## 1. Proje nedir

LINK Lojistik .NET iş başvurusu için portfolyo demosu: bir **Teslimat Kanıtı
(Proof of Delivery)** sistemi.

- **Backend (API)** — ASP.NET Core, JWT auth, teslimatlar + kanıt (foto/imza) akışı
- **Ops paneli (Web)** — MVC + Razor + DevExtreme, operasyon ekibi için
- **Sürücü uygulaması (Mobile)** — .NET MAUI, **sadece Android** (`net8.0-android`),
  offline kuyruk ile foto + imza + alıcı adı toplayıp API'ye senkronlar
- **Altyapı** — SQL Server 2022 + MinIO (S3 uyumlu obje deposu), Docker Compose

9 adımlık plan **kod olarak tamamlandı**. `docker compose up` uçtan uca doğrulandı.
22 unit + 5 integration test geçiyor. Git repo `main` üzerinde, **remote yok / push edilmedi**.

---

## 2. Şu an nerede kaldık (2026-08-29)

**Aktif iş:** MAUI sürücü uygulamasını gerçek cihazda test etmek.

- Emülatörde (pixel_7_-_api_34, arm64 Release APK) uçtan uca **UI akışı doğrulandı**:
  login → 3 teslimat → detay → foto çek → imza → alıcı adı → Kaydet → offline kuyruğa yazıldı.
- **Senkron (kuyruk → API + MinIO) emülatörde doğrulanamıyor** — emülatörde gerçek
  internet yok, `ProofSyncService` `NetworkAccess.Internet` guard'ında duruyor.
  → Gerçek telefon gerekiyor.
- İlk gerçek cihaz testinde **7 bug bulundu ve düzeltildi** (detay: maui-testing notu).
  Bu düzeltmeler **henüz commit edilmedi** — `git status` 8 modifiye Mobile dosyası:
  - `Controls/SignaturePadView.cs` — ScrollView içinde pan gesture çalınması (Android touch handler)
  - `MauiProgram.cs`, `Models/PendingProof.cs` — `[Unique]` index düzeltmesi
  - `Platforms/Android/AndroidManifest.xml` — targetSdk=33 (Android 14 receiver crash), `<queries>` bloğu
  - `Services/ApiClient.cs` — MinIO upload'da bare HttpClient (çift auth 400 hatası)
  - `Services/LocalDatabase.cs` — connection init race + `RetryFailedAsync()`
  - `Services/ProofSyncService.cs` — `RetryAllAsync()` (force sync, "Şimdi gönder")
  - `ViewModels/DeliveryListViewModel.cs`

**Bu oturumdaki seçim: gerçek telefon (R68T503HAQE) üzerinden USB reverse tunnel.**
- `.env` `MINIO_PUBLIC_ENDPOINT` → `http://localhost:9100` (USB tünel yolu).
- `api` `--force-recreate` ile yeniden başlatıldı, driver login 200.
- `adb -s R68T503HAQE reverse tcp:8080 tcp:8080` + `reverse tcp:9100 tcp:9100` kuruldu
  (`adb reverse --list` ile doğrulandı).
- `stay_on_while_plugged_in=3` ayarlandı (adb bağlantısı düşmesin).
- **Uygulama zaten güncel** — telefondaki APK bugün 15:20:45'te kuruldu, en son kaynak
  değişikliği 15:19:19'daydı; 7 fix'in hepsi içinde. Rebuild GEREKMEZ.

**Sıradaki adım:** Uygulamada "API adresi" alanı `http://127.0.0.1:8080` olmalı
(geçen oturumdan böyle kalmış olabilir). Driver login (`+905551112233` / `Driver123!`),
teslimat seç, foto+imza+ad, Kaydet, senkron olmasını bekle. Ops panelinden
(`http://localhost:8081`) kanıtın geldiğini doğrula.

_Wi-Fi'a dönmek istersen: `.env` `MINIO_PUBLIC_ENDPOINT=http://192.168.31.234:9100`,
`docker compose up -d --force-recreate api`, app API adresi `http://192.168.31.234:8080`.
PC güncel Wi-Fi IP: `192.168.31.234`. Firewall engel değil; risk router AP isolation._

---

## 3. Servisler nasıl çalışır

### Backend (Docker Compose) — `C:\Users\abdullahhaktan\source\Claude\logistic`

```bash
docker compose up -d                      # tüm stack
docker compose up -d --force-recreate api web   # sadece .env değişince
docker compose ps                         # durum
docker compose logs -f api                # loglar
docker compose down                       # durdur
```

| Servis    | Host portu | Ne için |
|-----------|-----------|---------|
| api       | `8080`    | REST API + Swagger (`http://localhost:8080/swagger`) |
| web       | `8081`    | Ops paneli (`http://localhost:8081`) |
| sqlserver | `1433`    | SQL Server 2022, DB `LinkLogistics` |
| minio     | `9100` (API), `9101` (console) | Obje deposu, bucket `proofs`, console `http://localhost:9101` |
| migrator  | —         | DbUp migration, başlarken çalışıp exit 0 verir |

Başlarken sıra: sqlserver+minio healthy → migrator çalışır+exit → api → web.

`.env` (gitignore'da; `.env.example` commit'li) önemli anahtarlar:
- `MINIO_PUBLIC_ENDPOINT` — **presigned URL'lerin host'u.** Mobil/tarayıcı bununla
  MinIO'ya bağlanır. Emülatör: `http://10.0.2.2:9100`. Gerçek telefon Wi-Fi:
  `http://<PC-LAN-IP>:9100`. Host tarayıcı: `http://localhost:9100`.
  Değiştirince `api`'yi force-recreate et.
- Demo login'ler: sürücü `+905551112233` / `Driver123!`, ops `+905550001122` / `Ops12345!`

### Mobil uygulama (MAUI, Android)

Ortam değişkenleri (makine geneli değil, terminal başına):
```
JAVA_HOME=C:\Program Files (x86)\Android\openjdk\jdk-17.0.14
ANDROID_HOME=C:\Program Files (x86)\Android\android-sdk
```
adb: `%ANDROID_HOME%\platform-tools\adb.exe`

Proje: `src/LinkLogistics.Mobile`, `net8.0-android`.

Çalıştırma seçenekleri:
- **Emülatör:** `emulator -avd pixel_7_-_api_34`, uygulama API adresi `http://10.0.2.2:8080`
- **Gerçek telefon, USB (en güvenilir):**
  `adb -s <serial> reverse tcp:8080 tcp:8080` **ve** `adb -s <serial> reverse tcp:9100 tcp:9100`,
  uygulama API adresi `http://127.0.0.1:8080`, `.env` `MINIO_PUBLIC_ENDPOINT=http://localhost:9100`
- **Gerçek telefon, Wi-Fi (şu anki seçim):** aynı ağ, API adresi `http://192.168.31.234:8080`,
  `.env` `MINIO_PUBLIC_ENDPOINT=http://192.168.31.234:9100`

APK build:
- Debug (`dotnet build -t:Install`) — Fast Deployment, assembly'ler `.__override__`'a push edilir;
  `adb install` veya `pm clear` ile crash eder ("No assemblies found").
- Release / standalone: `dotnet publish -c Release -p:RuntimeIdentifier=android-arm64 -p:AndroidPackageFormat=apk`
  (her şeyi gömer, Release'de logging provider YOK).

Fiziksel telefon: Samsung Galaxy A32 (SM-A325F, Android 12), serial `R68T503HAQE`.
adb bağlantısı ekran kilitlenince düşüyor → Developer options → "Stay awake".

---

## 4. Bilinen tuzaklar / notlar

- MAUI 8.0.3 çok eski; Android 14 receiver export bug'ı targetSdk=33 ile geçici çözüldü.
  Kalıcı çözüm: `Microsoft.Maui.Controls`'u ~8.0.7+ bump (workload update gerekli).
- Release APK'da log yok (`AddDebug` sadece `#if DEBUG`) → logcat'te sync logu görünmez.
- Yerel SQL Server (Docker dışı): sadece Windows auth, `Trusted_Connection=True`.
- `taskkill //F //IM dotnet.exe` KULLANMA — tüm dotnet süreçlerini öldürür.
- DbMigrator .sql dosyalarını embedded resource olarak gömer → script düzenleyince
  `--no-build`'den önce rebuild şart.
- CI Mobile'ı hariç tutar (`LinkLogistics.Backend.slnf`) çünkü ubuntu'da maui workload yok.

---

## 5. Kalan opsiyonel işler

- [ ] Gerçek telefonda foto+imza capture + sync uçtan uca doğrula
- [ ] Mobile bug-fix'lerini commit et (8 dosya)
- [ ] GitHub'a push (remote yok)
- [ ] README için ekran görüntüleri
- [ ] MAUI Controls paketini bump (targetSdk=33 workaround'ı kaldır)
