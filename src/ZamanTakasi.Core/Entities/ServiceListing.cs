using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;

namespace ZamanTakasi.Core.Entities;

public class ServiceListing
{
    public Guid Id { get; private set; }
    public Guid ProviderUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public SkillTier Tier { get; private set; }
    /// <summary>İlan içeriğinin dili ("en" / "tr"). Arayüz diline göre filtrelenir.</summary>
    public string Language { get; private set; } = "en";
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ServiceListing() { } // EF Core

    public ServiceListing(Guid providerUserId, string title, string description, SkillTier tier, string language = "en")
    {
        if (providerUserId == Guid.Empty) throw new DomainException("ProviderUserId zorunlu.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title zorunlu.");

        Id = Guid.NewGuid();
        ProviderUserId = providerUserId;
        Title = title.Trim();
        Description = (description ?? string.Empty).Trim();
        Tier = tier;
        Language = NormalizeLanguage(language);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Yalnızca desteklenen diller; geçersizse "en".</summary>
    private static string NormalizeLanguage(string? language)
    {
        var l = (language ?? "en").Trim().ToLowerInvariant();
        return l is "en" or "tr" ? l : "en";
    }

    /// <summary>1 saatlik fiyat (ZK) = 1 * (int)Tier.</summary>
    public int HourlyCreditRate => (int)Tier;

    public void Deactivate() => IsActive = false;
}
