# MtGArtRanker

Pick a Magic: The Gathering card, rank its unique illustrations from favorite to least favorite, and save your personal rankings backed by Azure SQL.

> **Status:** MVP scaffolding. Single-user, no auth yet. Microsoft Entra External ID is planned for v2.

## Features

- Search any MTG card via the Scryfall API (autocomplete, set browse, URL/ID paste).
- View **unique illustrations** for a card (deduped by `illustration_id`), treating each card as a whole.
- Rank illustrations with **drag-and-drop** _and_ editable numeric position fields.
- Persist your ordered list in Azure SQL; link back to Scryfall for every art.
- Mirrors metadata only for the **top 15 illustrations** by usage; everything else stays live.

## Architecture

```
React + Vite + TS (Azure Static Web Apps)
        │  /api/* (linked backend)
        ▼
ASP.NET Core 8 Web API (Azure App Service, Linux)
        │
   ┌────┴───────────────┬───────────────────┐
   ▼                    ▼                   ▼
Azure SQL DB     Azure Key Vault       Scryfall API
(EF Core)        (via Managed Identity) (live + cache)
```

See [`docs/architecture.md`](docs/architecture.md).

## Repo layout

```
src/api/      ASP.NET Core 8 Web API + EF Core
src/web/      React + Vite + TypeScript + dnd-kit
infra/        Bicep IaC (dev + prod)
.github/      CI + deploy workflows (OIDC, no static secrets)
docs/         Architecture and setup docs
```

## Local development

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- Azure CLI (`az`) and GitHub CLI (`gh`) for deploy
- Local SQL Server / LocalDB **or** Docker (`mcr.microsoft.com/azure-sql-edge`)

### API

```bash
cd src/api
dotnet restore
dotnet ef database update          # apply migrations to local DB
dotnet run                         # http://localhost:5080
```

### Web

```bash
cd src/web
npm install
npm run dev                        # http://localhost:5173, proxies /api → :5080
```

## Deploying to Azure

See [`docs/setup.md`](docs/setup.md) for one-time Azure setup (resource groups, Entra app for GitHub OIDC, secrets in GitHub).

```bash
# Dev (also runs automatically on push to main)
az deployment sub create \
  --location westus2 \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json
```

CI/CD via GitHub Actions:

- `ci.yml` — build & test on PRs
- `deploy-dev.yml` — push to `main` → dev
- `deploy-prod.yml` — tag `v*` → prod (with environment approval)

## Data model

| Table                 | Purpose                                                   |
| --------------------- | --------------------------------------------------------- |
| `Users`               | Single seed user for MVP; auth-ready for Entra later.     |
| `Rankings`            | One per (User, `oracle_id`).                              |
| `RankingItems`        | Ordered list of unique illustrations per ranking.         |
| `CardMetadataCache`   | Top-15 illustrations mirrored from Scryfall.              |

## License

[MIT](LICENSE). Not affiliated with Wizards of the Coast. Card data from [Scryfall](https://scryfall.com).