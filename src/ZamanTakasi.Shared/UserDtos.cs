namespace ZamanTakasi.Shared;

public record UserProfileDto(Guid Id, string DisplayName, int ReputationScore, DateTime CreatedAt, decimal Balance);
