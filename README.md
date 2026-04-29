# Trade Journal

A multi-user day-trading journal. Angular frontend, ASP.NET Core (.NET 10) backend,
PostgreSQL via EF Core, Google sign-in.

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
docker-compose.yml             local Postgres
```

`Api → Services → Data` is the only allowed dependency direction.
Controllers never touch `DbContext`; services use it directly.

## First-time setup

### 1. Postgres

If you have Docker:

```
docker compose up -d postgres
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
| GET    | `/api/trades`               | bearer   |
| POST   | `/api/trades`               | bearer   |
| PUT    | `/api/trades/{id}`          | bearer   |
| POST   | `/api/trades/{id}/close`    | bearer   |

## Behavior rules (v1)

- Equities only.
- Trades are created `Open` without exit fields.
- Closing requires `ExitAt` and `ExitPrice`.
- `RealizedPnl` is computed in the service layer:
  - Long:  `(ExitPrice - EntryPrice) * Quantity - (Fees ?? 0)`
  - Short: `(EntryPrice - ExitPrice) * Quantity - (Fees ?? 0)`
- All trade reads/writes are scoped to the authenticated user. Cross-user
  access returns 404.
