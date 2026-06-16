# Zaman Takası — MVP (.NET 10 + Blazor)

İnsanların yeteneklerini **zaman** para birimiyle takas ettiği pazaryeri MVP'si.
Temel mekanik: **1 saat hizmet = 1 zaman kredisi (ZK)**, uzmanlığa göre 1x–3x çarpan.

> **Krediler kapalı devredir:** hiçbir uçtan nakde/TL'ye çevrilemez. Bu kasıtlı bir
> hukuki tasarım kararıdır. Nakitle kredi satışı, KYC, sigorta vb. **kapsam dışıdır**
> (yalnızca `interface` + stub + `// TODO`).

## Mimari (tek solution)

| Proje | Sorumluluk |
|---|---|
| `ZamanTakasi.Core` | Saf domain: entity'ler, enum'lar, **ledger kuralları** (bağımlılıksız) |
| `ZamanTakasi.Shared` | API ↔ Web ortak DTO/sözleşmeler |
| `ZamanTakasi.Infrastructure` | EF Core (SQLite), Identity, repository/servisler, migration |
| `ZamanTakasi.Api` | ASP.NET Core Web API, JWT auth, Swagger, seed |
| `ZamanTakasi.Web` | Blazor Web App (Interactive Server) — API'yi `HttpClient` ile tüketir |
| `ZamanTakasi.Tests` | xUnit — ledger invariant'ları |

**Kullanıcı modeli:** Domain `User` (Core) ile `ApplicationUser : IdentityUser<Guid>`
(Infrastructure) **aynı Guid Id'yi paylaşır** (1:1). Böylece Core, Identity'ye bağımlı
olmadan saf kalır; auth ileride değiştirilebilir.

## Çekirdek iş kuralları (testle kanıtlı)

1. **Bakiye = kullanıcının `LedgerEntry.Amount` toplamı.** Ayrı "balance" alanı yok.
2. Booking **Completed** olunca **tek transaction**'da atomik 3 kayıt yazılır:
   requester `-CreditCost`, provider `+kazanç`, platform `+komisyon`. **Toplam her zaman 0.**
3. Durum akışı yalnızca **Pending → Accepted → Completed**; geçersiz geçiş reddedilir.
4. Yetersiz bakiyede negatife düşülemez → `400`.
5. Komisyon `appsettings` → `Ledger:CommissionRate` (varsayılan **0.13**).
6. Parasal yuvarlama: 2 ondalık, **banker's rounding** (`MidpointRounding.ToEven`).

## Gereksinimler
- .NET SDK **10.x** (`dotnet --info` ile doğrula)
- `dotnet-ef` (migration için): `dotnet tool install --global dotnet-ef`

## Çalıştırma

DB migration'ları **API ilk açılışta otomatik** uygular ve seed verisini ekler
(platform sistem hesabı + demo kullanıcılar/ilanlar). Elle migration için:

```bash
dotnet ef database update -p src/ZamanTakasi.Infrastructure
```

### 1) API (port 5053)
```bash
dotnet run --no-launch-profile --project src/ZamanTakasi.Api
# ASPNETCORE_URLS=http://localhost:5053  (veya launch profili)
```
- Swagger: **http://localhost:5053/swagger**

### 2) Web (port 5090)
```bash
dotnet run --no-launch-profile --project src/ZamanTakasi.Web
```
- Uygulama: **http://localhost:5090**
- Web'in API adresi: `src/ZamanTakasi.Web/appsettings.json` → `ApiBaseUrl`.

### Testler
```bash
dotnet test
```

## Demo akışı (UI'dan çekirdek döngü)
Demo hesaplar: **alice@demo.local** / **bob@demo.local**, parola **Passw0rd!**
(her ikisinde 10 ZK açılış bakiyesi).

1. **http://localhost:5090** → *Giriş / Kayıt* → `alice` ile giriş.
2. **İlanlar** → Bob'un "Web sitesi kurulumu" (Verified ×2) ilanına **1 saat** rezerve et → 2 ZK.
3. Çıkış yap, **bob** ile giriş → **Rezervasyonlarım** → *Kabul et* → *Tamamla*.
4. **Cüzdan**: bob `+1.74` ZK kazanır, alice `-2` ZK; platform komisyonu `0.26`.
   (alice 10→8, bob 10→11.74 — defter toplamı korunur.)

Aynı akış Swagger'dan da denenebilir: `auth/login` → token'ı **Authorize**'a gir →
`listings`, `bookings`, `wallet`.

## Güvenlik notu
JWT imza anahtarı `appsettings.json` içinde **yalnızca geliştirme** placeholder'ıdır.
Gerçek ortamda `dotnet user-secrets` veya ortam değişkeni ile **override edilmelidir**.

## Kapsam dışı (stub + TODO)
`IPaymentService` (nakitle kredi satışı), `IKycService` (kimlik doğrulama),
garanti fonu/sigorta, dinamik fiyatlama, bildirim/e-posta, gerçek zamanlı özellikler.
Bunlar bilinçli olarak implemente **edilmemiştir**.
