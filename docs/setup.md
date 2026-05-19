# Setup guide

This guide covers the one-time setup needed to run MtGArtRanker locally and deploy to Azure.

## 1. Local development

### Prerequisites

- .NET 8 SDK
- Node.js 20+

By default the API uses a **local SQLite file** for storage — no SQL Server,
LocalDB, or Docker required. The file `src/api/mtgartranker.db` is created on
first run (it's git-ignored).

### Steps

```bash
# Restore + build
dotnet build MtGArtRanker.slnx

# Run the API (http://localhost:5080, Swagger at /swagger).
# On first run it creates mtgartranker.db next to the API.
dotnet run --project src/api

# In a separate terminal, run the web app (http://localhost:5173)
cd src/web
npm install
npm run dev
```

The Vite dev server proxies `/api/*` to `http://localhost:5080`, so you can
browse to <http://localhost:5173>.

### Switching to SQL Server for local testing (optional)

If you'd rather use LocalDB / a containerized SQL Server, edit
`src/api/appsettings.Development.json`:

```jsonc
{
  "Database": { "Provider": "sqlserver", "AutoMigrate": true },
  "ConnectionStrings": {
    "Sql": "Server=(localdb)\\MSSQLLocalDB;Database=MtGArtRanker;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### Migrating your local SQLite data to a live SQL Server

There's a small CLI tool that copies everything from the local SQLite file into
an Azure SQL (or any SQL Server) database. It applies the EF Core migrations
on the destination first, then upserts users, rankings, ranking items, and the
metadata cache.

```bash
dotnet run --project src/tools/MigrateDb -- \
  --from "Data Source=src/api/mtgartranker.db" \
  --to   "Server=tcp:<your-sql>.database.windows.net,1433;Initial Catalog=mtgartranker;Authentication=Active Directory Default;Encrypt=True;"
```

- `--from` is the SQLite file (defaults to `Data Source=mtgartranker.db` in the
  current working directory).
- `--to` is the destination SQL Server connection string.
- Add `--force` if the destination already contains rankings and you want to
  merge into it.
- The destination defaults to **idempotent**: re-running won't duplicate
  rankings, and existing items are replaced.

## 2. Azure one-time setup

You'll need a Microsoft Entra tenant + an Azure subscription. The following walkthrough uses the Azure CLI.

### 2.1. Sign in

```bash
az login
az account set --subscription <subscription-id>
```

### 2.2. Create a GitHub OIDC service principal

This lets GitHub Actions deploy without storing client secrets.

```bash
# Replace <your-github-user>/<repo>
REPO="<your-github-user>/MtGArtRanker"
SUB_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

# 1) App registration + service principal
APP=$(az ad app create --display-name "mtgartranker-gha" --query appId -o tsv)
az ad sp create --id "$APP"
SP_OID=$(az ad sp show --id "$APP" --query id -o tsv)

# 2) Federated credentials for main + tags + environments
for SUBJECT in \
  "repo:$REPO:ref:refs/heads/main" \
  "repo:$REPO:ref:refs/tags/v*" \
  "repo:$REPO:environment:dev" \
  "repo:$REPO:environment:prod"; do
  az ad app federated-credential create --id "$APP" --parameters "{
    \"name\": \"$(echo $SUBJECT | tr ':/*' '---')\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"$SUBJECT\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
done

# 3) Owner at subscription scope (simplest; tighten later)
az role assignment create --assignee "$APP" --role Owner --scope "/subscriptions/$SUB_ID"

echo "AZURE_CLIENT_ID=$APP"
echo "AZURE_TENANT_ID=$TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID=$SUB_ID"
echo "DEPLOYER_PRINCIPAL_ID=$SP_OID"
```

### 2.3. Find your own Entra object id

```bash
az ad signed-in-user show --query id -o tsv     # your oid
az ad signed-in-user show --query userPrincipalName -o tsv  # your UPN
```

### 2.4. Create GitHub repo secrets

In the GitHub repo → Settings → Secrets and variables → Actions, create:

| Secret name                | Value                                                              |
| -------------------------- | ------------------------------------------------------------------ |
| `AZURE_CLIENT_ID`          | the `appId` from above                                             |
| `AZURE_TENANT_ID`          | your tenant id                                                     |
| `AZURE_SUBSCRIPTION_ID`    | your subscription id                                               |
| `SQL_ADMIN_PASSWORD`       | strong password (used only as a placeholder; Entra auth is primary)|
| `SQL_ENTRA_ADMIN_OID`      | your Entra object id (`oid`)                                       |
| `SQL_ENTRA_ADMIN_UPN`      | your Entra UPN (e.g. `you@example.com`)                            |
| `DEPLOYER_PRINCIPAL_ID`    | `$SP_OID` from above (the GHA service principal's object id)       |

Also create the GitHub environments `dev` and `prod`. Add a required-reviewer protection rule on `prod`.

### 2.5. Manual first deploy (optional)

To verify infra before relying on CI:

```bash
az deployment sub create \
  --location westus2 \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json \
  --parameters \
      sqlAdminPassword="<strong-password>" \
      sqlEntraAdminObjectId="$(az ad signed-in-user show --query id -o tsv)" \
      sqlEntraAdminLogin="$(az ad signed-in-user show --query userPrincipalName -o tsv)" \
      deployerPrincipalId="<sp-oid-from-2.2>"
```

### 2.6. Grant the App Service MI access to Azure SQL

After the first deploy, run this **once per environment** to create a contained user for the App Service's managed identity and grant it permission to read/write data:

```sql
-- Connect to the mtgartranker DB on sql-mtgartrank-<env>-xxxxx.database.windows.net
-- as the Entra admin (yourself).
CREATE USER [app-mtgartrank-<env>-xxxxx] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [app-mtgartrank-<env>-xxxxx];
ALTER ROLE db_datawriter ADD MEMBER [app-mtgartrank-<env>-xxxxx];
ALTER ROLE db_ddladmin   ADD MEMBER [app-mtgartrank-<env>-xxxxx];
```

Replace `app-mtgartrank-<env>-xxxxx` with the actual App Service name (output of the Bicep deployment).

> `db_ddladmin` is needed because EF Core migrations run on startup. If you prefer to run migrations out-of-band, remove that role and run `dotnet ef database update` manually.

## 3. Push to GitHub

```bash
git add .
git commit -m "Initial scaffold of MtGArtRanker MVP"
gh repo create MtGArtRanker --public --source . --remote origin --push
```

The push to `main` will trigger `Deploy (dev)`. To deploy prod, tag a release:

```bash
git tag v0.1.0
git push origin v0.1.0` 
