using ZamanTakasi.Core.Enums;

namespace ZamanTakasi.Shared;

public record CreateListingRequest(string Title, string Description, SkillTier Tier, string Language = "en");

public record ListingDto(
    Guid Id,
    Guid ProviderUserId,
    string ProviderDisplayName,
    string Title,
    string Description,
    SkillTier Tier,
    int HourlyCreditRate,
    bool IsActive,
    DateTime CreatedAt,
    string Language);
