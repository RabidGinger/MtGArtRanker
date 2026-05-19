# Architecture

## Overview

MtGArtRanker is a single-page web app for ranking the unique illustrations of any Magic: The Gathering card. The MVP is **single-user, no auth**; Microsoft Entra External ID is planned for v2.

```
┌──────────────────────────────────────────────┐
│                Browser (SPA)                  │
│           React + Vite + TypeScript           │
│         dnd-kit for drag-and-drop rank        │
└──────────────────┬───────────────────────────┘
                   │ HTTPS, /api/* routed by SWA
                   ▼
┌──────────────────────────────────────────────┐
│        Azure Static Web Apps (Standard)       │
│   Static hosting + "Bring your own API"       │
│   linked backend (App Service)                │
└──────────────────┬───────────────────────────┘
                   │ HTTPS (private SWA → App Service)
                   ▼
┌──────────────────────────────────────────────┐
│  Azure App Service (Linux, .NET 8)            │
│  ASP.NET Core Web API                         │
│  ├── Scryfall HTTP client (live + cache)      │
│  ├── EF Core 8 (SQL Server provider)          │
│  └── Managed Identity                         │
└────┬───────────────────────────┬─────────────┘
     │                           │
     ▼                           ▼
┌──────────────────┐    ┌─────────────────────┐
│  Azure SQL DB    │    │  Azure Key Vault    │
│  (Basic / S1)    │    │  Stores SQL conn    │
│  Entra-auth      │    │  string. App MI has │
│                  │    │  Secrets User role  │
└──────────────────┘    └─────────────────────┘

                 ┌──────────────────────┐
                 │   Scryfall API       │
                 │   (public, free)     │
                 └──────────────────────┘
```

## Components

### Frontend (`src/web`)
- React 18 + TypeScript + Vite.
- `@dnd-kit/core`, `@dnd-kit/sortable` for drag-and-drop reorder.
- Per-row numeric input lets the user set a card's rank position directly; the list reflows automatically.
- Card picker supports:
  - **Free-text autocomplete** (Scryfall `/cards/autocomplete` via our API).
  - **Pasting a Scryfall URL** (the URL slug is parsed into a card name).
  - **Pasting a Scryfall card id** (UUID).
- `staticwebapp.config.json` rewrites SPA navigation and excludes `/api/*` from the fallback so the linked backend handles those routes.

### API (`src/api`)
ASP.NET Core 8 Web API with the following controllers:

| Route                                  | Method | Purpose                                           |
| -------------------------------------- | ------ | ------------------------------------------------- |
| `/api/cards/search?q=...`              | GET    | Scryfall autocomplete passthrough.                |
| `/api/cards/{idOrName}`                | GET    | Resolve a card → `oracle_id` + name.              |
| `/api/cards/{idOrName}/printings`      | GET    | Unique illustrations for a card.                  |
| `/api/rankings`                        | GET    | List the user's rankings.                         |
| `/api/rankings/{oracleId}`             | GET    | Get a ranking.                                    |
| `/api/rankings/{oracleId}`             | PUT    | Upsert a ranking (replaces items, renumbers 1..N).|
| `/api/rankings/{oracleId}`             | DELETE | Delete a ranking.                                 |

Key services:

- `ScryfallClient` — typed `HttpClient` over `https://api.scryfall.com`, deduplicates printings by `illustration_id` (Scryfall `unique=art`), supports double-faced cards by falling back to `card_faces[0]` when top-level `illustration_id` / `image_uris` is missing.
- `RankingService` — EF Core repository. On save, the **top 15** items per ranking are mirrored into `CardMetadataCache` (Q10 of the design Q&A). Everything else is fetched live and cached in-memory (15-minute TTL).

### Data store

Azure SQL Database (Basic in dev, S1 in prod). Schema:

- `Users` (single seed row for MVP, `00000000-0000-0000-0000-000000000001`)
- `Rankings (UserId, OracleId)` — unique
- `RankingItems (RankingId, IllustrationId)` — unique; indexed by `(RankingId, Position)`
- `CardMetadataCache (IllustrationId)` — mirrors top-15 art metadata as JSON

Migrations are generated via `dotnet ef` and applied at app startup (configurable via `Database:AutoMigrate`).

### Configuration & secrets

- **Local dev**: `appsettings.Development.json` points EF Core at LocalDB.
- **Azure**: `KeyVault:Uri` app setting causes the app to load configuration from Key Vault using **Managed Identity**. The SQL connection string is stored as the Key Vault secret `ConnectionStrings--Sql` and uses `Authentication=Active Directory Default` so the App Service's MI authenticates to Azure SQL.
- The App Service's system-assigned MI is granted the `Key Vault Secrets User` role on the vault (via Bicep).

### Environments

- `dev` — resource group `rg-mtgartrank-dev`, Basic SQL, B1 App Service.
- `prod` — resource group `rg-mtgartrank-prod`, S1 SQL, S1 App Service, `alwaysOn=true`.

Both environments are deployed via Bicep at the subscription scope (`infra/main.bicep`).

### CI / CD

- `ci.yml` — builds API, web, validates Bicep on PRs and pushes.
- `deploy-dev.yml` — push to `main` deploys dev (infra → API → web).
- `deploy-prod.yml` — git tag `v*` deploys prod with required-reviewer environment protection.
- All Azure auth via **GitHub OIDC**; no long-lived secrets stored in GitHub.

## Future work (v2+)

- Microsoft Entra External ID (CIAM) for multi-user accounts.
- Ranking history (versioned snapshots).
- Pairwise/Elo ranking mode.
- Set browser for picking cards by set/release.
- Share a ranking via public read-only link.