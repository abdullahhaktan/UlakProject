# LinkLogistics — Teslimat Kanıtı / Proof of Delivery

> Portföy projesi · .NET 8 · ASP.NET Core · MSSQL + Dapper + Stored Procedure · REST API · jQuery + DevExtreme · .NET MAUI · Docker

Türkçe · [English below](#english)

---

## Sorun

Nakliye firmalarında sürücüler teslimat kanıtını hâlâ WhatsApp'tan fotoğraf atarak veya kağıt irsaliye
imzalatarak topluyor. Bu kayıtlar kayboluyor, aranamıyor ve müşteriye "teslim edildi" bilgisi gecikmeli gidiyor.

## Çözüm

- **Sürücü mobil uygulaması (.NET MAUI, Android):** teslimat anında **fotoğraf + imza + GPS + zaman damgası** kaydeder.
  İnternet yoksa kayıt cihazda kuyruğa alınır, bağlantı gelince otomatik senkronize olur.
- **Operasyon web paneli (ASP.NET Core MVC):** teslimatları oluşturur, sürücüye atar, kanıtları görüntüler,
  PDF/Excel olarak indirir.

**Ölçülebilir hedef:** kanıtın merkeze ulaşma süresi saatlerden saniyelere insin; kayıtlar sipariş numarasıyla
aranabilir olsun.

---

## Mimari

```
                         ┌──────────────────────────┐
  ┌───────────────┐      │   LinkLogistics.Api      │      ┌──────────────┐
  │ Mobile (MAUI) │─────▶│   REST + JWT + Swagger   │─────▶│ MSSQL        │
  │  sürücü       │ HTTP │                          │ SP   │ (Dapper)     │
  └───────────────┘      │   ┌──────────────────┐   │      └──────────────┘
  ┌───────────────┐      │   │ Infrastructure   │   │      ┌──────────────┐
  │ Web (MVC)     │─────▶│   │ Dapper · MinIO   │   │─────▶│ MinIO (S3)   │
  │  operasyon    │ HTTP │   │ QuestPDF·ClosedXML│   │      │ foto + imza  │
  └───────────────┘      │   └──────────────────┘   │      └──────────────┘
                         └──────────────────────────┘
```

Katmanlı tek solution:

| Proje | Sorumluluk |
|---|---|
| `LinkLogistics.Shared` | API ↔ istemci DTO'ları, offline kuyruk retry politikası |
| `LinkLogistics.Core` | Domain modelleri, repository/servis arayüzleri |
| `LinkLogistics.Infrastructure` | Dapper + Stored Procedure çağrıları, MinIO (AWS S3 SDK), JWT üretimi, PBKDF2, QuestPDF, ClosedXML |
| `LinkLogistics.Api` | REST uçları, JWT bearer, FluentValidation, `ProblemDetails`, Serilog |
| `LinkLogistics.Web` | Operasyon paneli — MVC + Razor + jQuery + Bootstrap 5 + DevExtreme; cookie auth; iki dilli (TR/EN) |
| `LinkLogistics.Mobile` | .NET MAUI sürücü uygulaması (net8.0-android); MVVM; SQLite offline kuyruk |
| `LinkLogistics.DbMigrator` | DbUp ile idempotent `.sql` migration (compose'da otomatik) |
| `tests/*` | xUnit birim testleri + Testcontainers entegrasyon testleri |

Veri erişimi tamamen **stored procedure** üzerinden (`db/scripts/020_stored_procedures.sql`). EF Core kullanılmaz.

---

## Çalıştırma

Gereksinim: **Docker** + **Docker Compose**.

```bash
cp .env.example .env
docker compose up --build
```

| Servis | Adres | Giriş |
|---|---|---|
| Operasyon paneli | http://localhost:8081 | `+905550001122` / `Ops12345!` |
| API + Swagger | http://localhost:8080/swagger | — |
| MinIO konsolu | http://localhost:9101 | `linklogistics` / `linklogistics-secret` |

Demo sürücü hesapları: `+905551112233` / `Driver123!` · `+905554445566` / `Driver123!`

Durdurma: `docker compose down` (veriyi de sil: `docker compose down -v`).

### Sürücü uygulaması (MAUI)

```bash
export JAVA_HOME=".../openjdk/jdk-17"
export ANDROID_HOME=".../android-sdk"
dotnet build src/LinkLogistics.Mobile/LinkLogistics.Mobile.csproj -f net8.0-android -c Release
```

APK: `src/LinkLogistics.Mobile/bin/Release/net8.0-android/com.linklogistics.driver-Signed.apk`
Emülatörde API adresi `http://10.0.2.2:8080`'dir (giriş ekranından değiştirilebilir).

### Testler

```bash
dotnet test LinkLogistics.Backend.slnf          # birim + entegrasyon (Testcontainers, Docker gerekir)
```

---

## İlan gereksinimleri → nerede karşılandı

| İlan | Bu projede |
|---|---|
| C#, .NET / ASP.NET Core | `LinkLogistics.Api`, `LinkLogistics.Web` (net8.0) |
| MSSQL, T-SQL | `db/scripts/001_schema.sql`, ilişkisel model, CHECK/UNIQUE kısıtlar, indeksler |
| Stored Procedure | `db/scripts/020_*.sql` — tüm iş işlemleri SP; idempotent kanıt kaydı, yetki kontrolü, sayfalı arama, TVP |
| Dapper | `src/LinkLogistics.Infrastructure/Persistence/*` |
| REST API + JSON | `LinkLogistics.Api` — Swagger, `ProblemDetails`, JWT + refresh token |
| İlişkisel veri tabanı | 6 tablo + FK'ler, cascade, computed/persisted kolonlar |
| HTML/CSS/JS/jQuery | `src/LinkLogistics.Web/Views/**` — jQuery AJAX, DevExtreme DataGrid CustomStore |
| DevExtreme | Deliveries/Proofs DataGrid (sunucu-taraflı sayfalama), Dashboard Chart |
| Bootstrap | Panel arayüzü Bootstrap 5 |
| Harici sistem entegrasyonu | MinIO (S3 uyumlu) presigned URL ile doğrudan dosya yükleme |
| Excel | `ProofDocumentService` — kanıt listesi `.xlsx` export (ClosedXML) |
| PDF | `ProofDocumentService` — tek sayfa teslimat kanıtı PDF'i (QuestPDF) |
| XML | JWT/REST JSON esas; UBL-benzeri XML çıktı önceki tasarımda vardı, POD kapsamında dosya odaklı kalındı |
| Mevcut projeyi okuyup geliştirme | Katmanlı mimari, versiyonlu migration (`021_*.sql`), net sınırlar |

---

## Kapsam dışı (bilinçli)

Rota optimizasyonu · canlı sürücü takibi · çoklu firma (multi-tenant) · müşteri portalı · iOS yayını ·
barkod okuma · ERP entegrasyonu. Bunlar ayrı faz olarak ele alınır.

---

<a name="english"></a>

## English

**Proof of Delivery system.** Drivers capture photo + signature + GPS + timestamp at delivery time from a
**.NET MAUI** Android app (with an offline SQLite queue that syncs when back online); operations staff manage
deliveries and review/export proofs from an **ASP.NET Core MVC** panel.

Built to mirror a job posting's stack: **ASP.NET Core, C#, MSSQL + T-SQL + Stored Procedures, Dapper, REST API,
jQuery, DevExtreme, Bootstrap, Excel/PDF**. Data access is **stored-procedure only** — no EF Core.

Run everything with `cp .env.example .env && docker compose up --build` → panel on `:8081`, API on `:8080/swagger`.

Key design points:

- **Idempotent proof submission** — `POST /proofs` dedupes on a client-generated `ClientUuid`, so the offline
  queue can safely re-send. (`db/scripts/020_stored_procedures.sql` → `usp_Proof_Create`)
- **Direct-to-storage uploads** — the API issues presigned MinIO URLs; photo bytes never pass through it.
- **Authorization enforced in the database** — a driver only ever sees / acts on their own deliveries, checked
  inside the stored procedures, not just at the controller.
- **Offline queue guarantees** — a captured proof is written to disk + SQLite before the driver is told
  "saved"; a row is deleted only after a confirmed sync; failures back off exponentially (2→4→8→16→32 s, capped
  at 5 min) and surface a visible error after 5 attempts. Data is never lost silently.

Tests: `dotnet test LinkLogistics.Backend.slnf` — xUnit unit tests + Testcontainers integration tests that run
the real migration scripts against a throwaway SQL Server and verify the idempotency and authorization rules
end-to-end.

## License

MIT. Uses QuestPDF Community (free for organizations under $1M USD annual revenue) and a DevExtreme trial build
(watermark after 30 days — fine for a demo).
