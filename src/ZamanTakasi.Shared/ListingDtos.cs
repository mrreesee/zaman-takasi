using ZamanTakasi.Core.Enums;

namespace ZamanTakasi.Shared;

public record CreateListingRequest(string Title, string Description, SkillTier Tier);

public record ListingDto(
    Guid Id,
    Guid ProviderUserId,
    string ProviderDisplayName,
    string Title,
    string Description,
    SkillTier Tier,
    int HourlyCreditRate,
    bool IsActive,
    DateTime CreatedAt);
