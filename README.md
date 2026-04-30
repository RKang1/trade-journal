# Trade Journal

A multi-user day-trading journal. Angular frontend, ASP.NET Core (.NET 10) backend,
PostgreSQL via EF Core, Google sign-in.

This repo owns its own container deployment. On `rkserver`, it is expected to
publish the frontend and API only on loopback and sit behind nginx at
`journal.kangrc.com`.

## Prerequisites

- .NET 10 SDK
- Node 22+ / npm 11+
- PostgreSQL 16 (locally or via Docker / Docker Desktop)
- A Google OAuth 2.0 Web Client ID

## Repo layout

```
backend/
  src/
    TradeJournal.Api/         controllers, DTOs, JWT + CORS + DI
    TradeJournal.Services/    auth, trade rules, P&L, validation
    TradeJournal.Data/        EF entities, DbContext, migrations
  tests/
    TradeJournal.Services.Tests/
    TradeJournal.Api.Tests/
frontend/
  src/app/
    core/                     api types, auth, http interceptor, services
    pages/journal/            single-page journal UI
docker-compose.yml             app stack + optional local Postgres profile
deploy.sh                      pull + rebuild helper
```

`Api → Services → Data` is the only allowed dependency direction.
Controllers never touch `DbContext`; services use it directly.

## First-time setup

### 1. Postgres

If you have Docker:

```
docker compose --profile local-db up -d postgres
```

Otherwise install Postgres locally and create:

```
db:        trade_journal
user:      trade_journal
password:  trade_journal
port:      5432
```

Adjust `backend/src/TradeJournal.Api/appsettings.Development.json` if your local
Postgres uses different credentials.

### 2. Configure Google OAuth + JWT

Copy the committed example to a local-only file (gitignored) and fill in the
real values:

```
cp backend/src/TradeJournal.Api/appsettings.example.json \
   backend/src/TradeJournal.Api/appsettings.local.json
```

Then edit `appsettings.local.json` and replace the two placeholders:

```json
"Auth": {
  "GoogleClientId": "<your-client-id>.apps.googleusercontent.com",
  "JwtSigningKey": "<at least 32 random bytes of UTF-8>"
}
```

`appsettings.local.json` is loaded after `appsettings.json` and overrides it.
It is gitignored, so secrets never leave your machine.

The Google Client ID is created in the
[Google Cloud Console](https://console.cloud.google.com) under
`APIs & Services → Credentials → OAuth 2.0 Client IDs`. For local development
add `http://localhost:4200` as an authorized JavaScript origin.

Generate a JWT signing key:

```
openssl rand -base64 48
```

Then edit `frontend/src/environments/environment.ts` and put the **same**
Google Client ID into `googleClientId`.

For container-based deploys, copy `.env.example` to `.env` and fill in the same
values there:

```
cp .env.example .env
```

### 3. Apply database migrations

```
cd backend
dotnet ef database update --project src/TradeJournal.Data --startup-project src/TradeJournal.Api
```

### 4. Run

In one terminal:

```
cd backend
dotnet run --project src/TradeJournal.Api
```

In another:

```
cd frontend
npm install   # first time only
npm start
```

The API listens on `http://localhost:5211`. The Angular dev server runs on
`http://localhost:4200`.

## Container deploy

For a reverse-proxy deploy with the app publishing to loopback:

```bash
cp .env.example .env
# fill in TRADE_JOURNAL_DB_CONNECTION, TRADE_JOURNAL_GOOGLE_CLIENT_ID,
# TRADE_JOURNAL_JWT_SIGNING_KEY, and the published port values you want

docker compose up -d --build
```

Default container publishing is:

- frontend: `127.0.0.1:14200 -> 80`
- api: `127.0.0.1:15211 -> 8080`

On `rkserver`, those defaults match the host nginx and DNS setup for
`journal.kangrc.com`.

For updates on a deployed host:

```bash
./deploy.sh
```

`deploy.sh` pulls the repo, then runs `docker compose up -d --build`. If a
host wrapper exists one directory up at `../trade-journal-compose.sh`, it uses
that instead so host-specific overrides can still hook in later without
changing the app workflow.

When the shared Docker network used by the host-managed Postgres stack exists
(default: `app-db`), `deploy.sh` also includes `docker-compose.shared-db.yml`
so the API container can resolve `postgres` on that network. You can override
the network name with `TRADE_JOURNAL_SHARED_DB_NETWORK`.

## Tests

```
# backend
cd backend && dotnet test

# frontend
cd frontend && npm test
```

## API

| Method | Route                       | Auth     |
| ------ | --------------------------- | -------- |
| POST   | `/api/auth/google`          | none     |
| GET    | `/api/auth/me`              | bearer   |
| GET    | `/api/auth/tokens`          | jwt only |
| POST   | `/api/auth/tokens`          | jwt only |
| DELETE | `/api/auth/tokens/{id}`     | jwt only |
| GET    | `/api/trades`               | bearer   |
| POST   | `/api/trades`               | bearer   |
| PUT    | `/api/trades/{id}`          | bearer   |
| POST   | `/api/trades/{id}/close`    | bearer   |

## External client flow

The trade endpoints already act on the authenticated user, so outside apps do
not need any special integration path. The intended flow is:

1. The user signs in to Trade Journal normally with Google and gets a journal
   JWT from `POST /api/auth/google`.
2. That JWT is used once to create a user-owned API token with
   `POST /api/auth/tokens`.
3. The outside app stores the returned API token and uses it as
   `Authorization: Bearer <token>` when calling `/api/trades`.

API tokens are scoped to the owning user, can be listed/revoked, and cannot be
used to mint more API tokens.

Example:

```bash
# 1. Create a long-lived API token for the current signed-in user.
curl -X POST https://journal.example.com/api/auth/tokens \
  -H "Authorization: Bearer <journal-jwt>" \
  -H "Content-Type: application/json" \
  -d '{ "name": "Day trader sync" }'

# 2. Use that API token from another app to create a trade for the same user.
curl -X POST https://journal.example.com/api/trades \
  -H "Authorization: Bearer tj_pat_..." \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "NVDA",
    "side": "Long",
    "entryAt": "2026-04-30T22:00:00Z",
    "entryPrice": 901.25,
    "quantity": 1,
    "fees": null,
    "setup": "breakout",
    "notes": "synced from day trader"
  }'
```

## Behavior rules (v1)

- Equities only.
- Trades are created `Open` without exit fields.
- Closing requires `ExitAt` and `ExitPrice`.
- `RealizedPnl` is computed in the service layer:
  - Long:  `(ExitPrice - EntryPrice) * Quantity - (Fees ?? 0)`
  - Short: `(EntryPrice - ExitPrice) * Quantity - (Fees ?? 0)`
- All trade reads/writes are scoped to the authenticated user. Cross-user
  access returns 404.
