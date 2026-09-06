using ZamanTakasi.Core.Exceptions;

namespace ZamanTakasi.Core.Entities;

/// <summary>
/// Domain kullanıcısı (saf POCO). Parola/kimlik bilgisi BURADA TUTULMAZ; o, Infrastructure'daki
/// ApplicationUser (ASP.NET Core Identity) üzerindedir. İkisi AYNI Guid Id'yi paylaşır (1:1).
/// Bakiye burada tutulmaz; her zaman LedgerEntry toplamından hesaplanır (tek doğruluk kaynağı defter).
/// </summary>
public class User
{
    public const int DisplayNameMaxLength = 120; // DB sütun sınırıyla aynı

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public int ReputationScore { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { } // EF Core

    public User(Guid id, string displayName)
    {
        if (id == Guid.Empty) throw new DomainException("User id boş olamaz.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("DisplayName zorunlu.");
        if (displayName.Trim().Length > DisplayNameMaxLength)
            throw new DomainException($"DisplayName en fazla {DisplayNameMaxLength} karakter olabilir.");

        Id = id;
        DisplayName = displayName.Trim();
        ReputationScore = 100; // başlangıç itibar puanı
        CreatedAt = DateTime.UtcNow;
    }
}
