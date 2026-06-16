# Zaman Takası — MVP (.NET 10 + Blazor)

A marketplace where people trade skills using **time** as currency.
Core mechanic: **1 hour of service = 1 time credit (ZK)**, with a 1x–3x multiplier by expertise tier.

> **Credits are closed-loop:** they can never be converted to cash/fiat at any point.
> This is a deliberate legal design decision. Cash credit sales, KYC, insurance, etc.
> are **out of scope** (interface + stub + `// TODO` only).

## Architecture (single solution)

| Project | Responsibility |
|---|---|
| `ZamanTakasi.Core` | Pure domain: entities, enums, **ledger rules** (no dependencies) |
| `ZamanTakasi.Shared` | DTOs / contracts shared between API and Web |
| `ZamanTakasi.Infrastructure` | EF Core (SQLite), Identity, repositories/services, migrations |
| `ZamanTakasi.Api` | ASP.NET Core Web API, JWT auth, Swagger, seed |
| `ZamanTakasi.Web` | Blazor Web App (Interactive Server) — consumes the API via `HttpClient` |
| `ZamanTakasi.Tests` | xUnit — ledger invariants |

**User model:** the domain `User` (Core) and `ApplicationUser : IdentityUser<Guid>`
(Infrastructure) **share the same Guid Id** (1:1). This keeps Core free of any Identity
dependency, so the auth layer can be swapped later without touching the domain.

## Core business rules (proven by tests)

1. **Balance = the sum of a user's `LedgerEntry.Amount`.** There is no separate "balance" field.
2. When a booking is **Completed**, three entries are written atomically in a **single transaction**:
   requester `-CreditCost`, provider `+earning`, platform `+commission`. **The sum is always 0.**
3. Status flow is strictly **Pending → Accepted → Completed**; invalid transitions are rejected.
4. Balance can never go negative → `400`.
5. Commission comes from `appsettings` → `Ledger:CommissionRate` (default **0.13**).
6. Monetary rounding: 2 decimals, **banker's rounding** (`MidpointRounding.ToEven`).

## Requirements
- .NET SDK **10.x** (verify with `dotnet --info`)
- `dotnet-ef` (for migrations): `dotnet tool install --global dotnet-ef`

## Running

The API **applies migrations automatically on startup** and seeds data
(platform system account + demo users/listings). To run migrations manually:

```bash
dotnet ef database update -p src/ZamanTakasi.Infrastructure
```

### 1) API (port 5053)
```bash
dotnet run --no-launch-profile --project src/ZamanTakasi.Api
# ASPNETCORE_URLS=http://localhost:5053  (or use the launch profile)
```
- Swagger: **http://localhost:5053/swagger**

### 2) Web (port 5090)
```bash
dotnet run --no-launch-profile --project src/ZamanTakasi.Web
```
- App: **http://localhost:5090**
- The API base address is set in `src/ZamanTakasi.Web/appsettings.json` → `ApiBaseUrl`.

### Tests
```bash
dotnet test
```

## Demo flow (core loop from the UI)
Demo accounts: **alice@demo.local** / **bob@demo.local**, password **Passw0rd!**
(both start with a 10 ZK opening balance).

1. **http://localhost:5090** → *Login* as `alice`.
2. **Listings** → book Bob's "Web sitesi kurulumu" (Verified ×2) for **1 hour** → 2 ZK.
3. Log out, log in as **bob** → **My bookings** → *Accept* → *Complete*.
4. **Wallet**: bob earns `+1.74` ZK, alice spends `-2` ZK; platform commission `0.26`.
   (alice 10→8, bob 10→11.74 — the ledger always balances.)

The same flow can be exercised from Swagger: `auth/login` → paste the token into
**Authorize** → `listings`, `bookings`, `wallet`.

## Security note
The JWT signing key in `appsettings.json` is a **development-only** placeholder.
In real environments it **must be overridden** via `dotnet user-secrets` or an
environment variable.

## Out of scope (stub + TODO)
`IPaymentService` (cash credit sales), `IKycService` (identity verification),
guarantee fund/insurance, dynamic pricing, notifications/email, real-time features.
These are intentionally **not** implemented.
