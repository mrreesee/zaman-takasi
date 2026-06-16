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
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ServiceListing() { } // EF Core

    public ServiceListing(Guid providerUserId, string title, string description, SkillTier tier)
    {
        if (providerUserId == Guid.Empty) throw new DomainException("ProviderUserId zorunlu.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title zorunlu.");

        Id = Guid.NewGuid();
        ProviderUserId = providerUserId;
        Title = title.Trim();
        Description = (description ?? string.Empty).Trim();
        Tier = tier;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>1 saatlik fiyat (ZK) = 1 * (int)Tier.</summary>
    public int HourlyCreditRate => (int)Tier;

    public void Deactivate() => IsActive = false;
}
