using Microsoft.Extensions.Caching.Memory;

namespace ZamanTakasi.Api.Auth;

/// <summary>
/// Kimlik uçları için hafif, bellek içi sabit-pencere eşik sayacı (fixed window).
/// Neden IP tabanlı değil: Web (Blazor Server) → API çağrıları hep aynı iç ağ adresinden gelir;
/// bu yüzden anahtar, hedefi tanımlayan değerdir (e-posta) ya da uç geneli sayaçtır.
/// Tek API örneği varsayımıyla IMemoryCache yeterlidir; ölçeklenince Redis'e taşınır.
/// </summary>
public sealed class AuthThrottle
{
    private readonly IMemoryCache _cache;
    public AuthThrottle(IMemoryCache cache) => _cache = cache;

    /// <summary>Pencere içinde <paramref name="limit"/> aşılmadıysa true; aşıldıysa false.</summary>
    public bool TryAcquire(string key, int limit, TimeSpan window)
    {
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Count) <= limit;
    }

    /// <summary>E-posta anahtarını normalize eder (büyük/küçük harf ve boşluk farkları aynı sayaca düşsün).</summary>
    public static string EmailKey(string scope, string? email)
        => $"{scope}:{(email ?? "").Trim().ToLowerInvariant()}";

    private sealed class Counter { public int Count; }
}
