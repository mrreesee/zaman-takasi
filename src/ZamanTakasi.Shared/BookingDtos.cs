using ZamanTakasi.Core.Enums;

namespace ZamanTakasi.Shared;

public record CreateBookingRequest(Guid ListingId, decimal Hours);

public record BookingDto(
    Guid Id,
    Guid ListingId,
    Guid RequesterUserId,
    Guid ProviderUserId,
    decimal Hours,
    decimal CreditCost,
    BookingStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);
