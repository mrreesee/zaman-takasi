# Zaman Takası — MVP (.NET 10 + Blazor + PostgreSQL)

A marketplace where people trade skills using **time** as currency.
Core mechanic: **1 hour of service = 1 time credit (ZK)**, with a 1x–3x multiplier by expertise tier.

> **Credits are closed-loop:** they can never be converted to cash/fiat at any point.
> ZK **cannot be purchased** with money — it is only earned by providing services
> (plus a one-time welcome balance at sign-up). This is a deliberate legal design decision.

## Architecture (single solution)

| Project | Responsibility |
|---|---|
| `ZamanTakasi.Core` | Pure domain: entities, enums, **ledger rules**, abstractions (no dependencies) |
| `ZamanTakasi.Shared` | DTOs / contracts shared between API and Web |
| `ZamanTakasi.Infrastructure` | EF Core (**PostgreSQL/Npgsql**), Identity, services, migrations, stubs |
| `ZamanTakasi.Api` | ASP.NET Core Web API, JWT auth, Swagger, seed, **Serilog** |
| `ZamanTakasi.Web` | Blazor Web App (Interactive Server) — consumes the API via `HttpClient` |
| `ZamanTakasi.Tests` | xUnit — ledger invariants + **concurrency (Testcontainers)** |

**User model:** the domain `User` (Core) and `ApplicationUser : IdentityUser<Guid>`
(Infrastructure) **share the same Guid Id** (1:1). Core stays free of any Identity dependency.

## Core business rules (proven by tests)

1. **Balance = the sum of a user's `LedgerEntry.Amount`.** There is no separate "balance" field.
2. When a booking is **Completed**, three entries are written atomically in a **single
   `Serializable` transaction**: requester `-CreditCost`, provider `+earning`, platform
   `+commission`. **The sum is always 0.** Concurrent completions that would overdraw are
   rejected (serialization retry → `400`/`409`); balance never goes negative.
3. Status flow is strictly **Pending → Accepted → Completed** (+ Cancelled).
4. Commission: `appsettings → Ledger:CommissionRate` (default **0.13**). Rounding: 2 decimals,
   banker's rounding (`MidpointRounding.ToEven`).
5. **Welcome balance:** on registration a one-time `OpeningBalance` entry is written
   (`Ledger:WelcomeBalance`, default **3 ZK**), idempotently. Injected liquidity does **not**
   sum to zero system-wide — by design.

## Requirements
- .NET SDK **10.x** (`dotnet --info`)
- **Docker** (for a local PostgreSQL — see below)
- `dotnet-ef` (migrations): `dotnet tool install --global dotnet-ef`

## Configuration (environment-driven)

All sensitive/variable settings are read from the environment, so switching hosts is just an
env change — never a code change.

| Setting | Source | Notes |
|---|---|---|
| Database | `DATABASE_URL` **or** `ConnectionStrings:Default` | `DATABASE_URL` may be a URI (`postgresql://user:pass@host:port/db`) — it is auto-converted to Npgsql key-value form |
| JWT signing key | `Jwt:Key` (env `Jwt__Key`) | **required**; dev value via user-secrets |
| Demo seed password | `Seed:DemoPassword` | if unset, demo users are not seeded |
| Welcome balance | `Ledger:WelcomeBalance` | default `3` |
| Public port (cloud) | `PORT` | Web binds to it automatically (Railway/Heroku style) |
| Web → API base URL | `ApiBaseUrl` | e.g. internal API URL in production |

## Running locally

### 1) Start PostgreSQL (Docker)
```bash
docker run -d --name zamantakasi-pg \
  -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=zamantakasi \
  -p 5432:5432 postgres:16-alpine
```

### 2) Local secrets (once, in the Api project)
```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=zamantakasi;Username=postgres;Password=postgres" --project src/ZamanTakasi.Api
dotnet user-secrets set "Jwt:Key" "DEV-ONLY-local-secret-change-me" --project src/ZamanTakasi.Api
dotnet user-secrets set "Seed:DemoPassword" "Passw0rd!" --project src/ZamanTakasi.Api
```

### 3) API (port 5053)
```bash
dotnet run --project src/ZamanTakasi.Api --launch-profile http
```
- Applies migrations + seeds automatically on startup. Swagger: **http://localhost:5053/swagger**

### 4) Web (port 5090)
```bash
dotnet run --project src/ZamanTakasi.Web --launch-profile http
```
- App: **http://localhost:5090** (API base in `appsettings.json` → `ApiBaseUrl`)

### Tests
```bash
dotnet test
```
Ledger/booking/rounding tests are pure domain (no DB). The **concurrency** and **welcome-balance**
tests use **Testcontainers** — they spin up a throwaway PostgreSQL and are **auto-skipped if
Docker is not running** (the rest of the suite still passes).

## Demo flow (core loop)
Demo accounts: **alice@demo.local** / **bob@demo.local**, password **Passw0rd!** (10 ZK each).
1. Login as `alice` → **Listings** → book Bob's "Web sitesi kurulumu" for 1h → 2 ZK.
2. Log in as **bob** → **Rezervasyonlarım** → *Accept* → *Complete*.
3. **Cüzdan**: bob `+1.74` ZK, alice `-2` ZK, commission `0.26` — the ledger always balances.

## Deploying to Railway

The app is designed for a **single public origin**: the browser only talks to the Blazor **Web**
app (over SignalR); the **API** is consumed server-side, so it runs as a **private** service.

Topology (one Railway project):
- **PostgreSQL** plugin → provides `DATABASE_URL`.
- **API** service (no public domain): env `DATABASE_URL` (from the PG plugin), `Jwt__Key`,
  `ASPNETCORE_URLS=http://0.0.0.0:8080`. Reached internally at `http://<api>.railway.internal:8080`.
- **Web** service (public domain): env `ApiBaseUrl=http://<api>.railway.internal:8080`. Binds to
  Railway's `PORT` automatically.

Each service's **Root Directory** is set to its project folder
(`src/ZamanTakasi.Api` / `src/ZamanTakasi.Web`); Railway's Nixpacks builds and runs the .NET app.
See the deployment walkthrough for click-by-click steps.

## Security note
No secret is committed. Locally use **user-secrets**; in production set env vars. The JWT key
**must** be a strong, unique value in any real environment.

## Out of scope (stub + TODO)
`IPaymentService`, `IKycService`, `INotificationService` (real email/push — currently logs only),
guarantee fund, dynamic pricing, real-time features. Intentionally **not** implemented.
