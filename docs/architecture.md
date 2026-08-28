# Architecture notes

## Data model

```mermaid
erDiagram
    Company ||--o{ AppUser : employs
    Company ||--o{ Delivery : owns
    AppUser ||--o{ Delivery : "assigned (driver)"
    AppUser ||--o{ RefreshToken : has
    AppUser ||--o{ Proof : "captured by"
    Delivery ||--o| Proof : "has one"
    Proof ||--o{ ProofPhoto : contains

    AppUser {
        int Id PK
        string Phone UK
        string PasswordHash "PBKDF2-SHA256"
        string Role "Driver | Ops"
    }
    Delivery {
        int Id PK
        string OrderRef UK
        string Status "Pending | Delivered | Failed"
        int AssignedDriverId FK
        decimal Lat
        decimal Lng
    }
    Proof {
        bigint Id PK
        guid ClientUuid UK "idempotency key"
        string Status "Delivered | Failed"
        string SignatureUrl
        datetime CapturedAtUtc
        datetime SyncedAtUtc
    }
    ProofPhoto {
        bigint Id PK
        string Url
        int OrderIndex "0..4"
    }
```

## Proof capture — offline path

```mermaid
sequenceDiagram
    actor Driver
    participant App as MAUI app
    participant Q as SQLite queue
    participant Sync as ProofSyncService
    participant API
    participant S3 as MinIO

    Driver->>App: photo + signature + submit
    App->>App: compress photos < 2 MB (SkiaSharp)
    App->>Q: write files + rows (state = Pending)
    App-->>Driver: "kaydedildi, gönderilecek"

    loop every 20s / on connectivity
        Sync->>Q: get due rows
        Sync->>API: POST /uploads/presign
        API-->>Sync: presigned PUT url
        Sync->>S3: PUT bytes
        Sync->>API: POST /proofs (ClientUuid)
        alt success
            API-->>Sync: 201 (or 200 duplicate)
            Sync->>Q: delete row + local files
        else transient error
            Sync->>Q: attempts++, backoff 2^n s (cap 5 min)
        else 4xx or attempts == 5
            Sync->>Q: state = Failed (visible, kept)
        end
    end
```

## Why these choices

- **Stored procedures for every write** — the job posting calls for T-SQL + Dapper + SP experience, and it
  keeps transactional logic (idempotency, one-proof-per-delivery, driver ownership) in one place.
- **Two hosts (Api + Web)** — both the panel and the mobile app consume the same REST API; the API is the
  single source of truth and the only thing that touches the database.
- **Presigned URLs** — large photo uploads bypass the API process entirely.
- **`ClientUuid` idempotency** — the offline queue may POST the same proof more than once (retry after a
  dropped response); the API dedupes so exactly one record is ever created.
- **Versioned migrations (`020_` then `021_`)** — changes ship as new numbered scripts, not edits to shipped
  ones, mirroring real deployment discipline.
