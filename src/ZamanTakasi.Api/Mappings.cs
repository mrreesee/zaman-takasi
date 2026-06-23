using ZamanTakasi.Core.Entities;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Api;

public static class Mappings
{
    public static BookingDto ToDto(this Booking b) =>
        new(b.Id, b.ListingId, b.RequesterUserId, b.ProviderUserId, b.Hours, b.CreditCost, b.Status, b.CreatedAt, b.CompletedAt);

    public static ListingDto ToDto(this ServiceListing l, string providerName) =>
        new(l.Id, l.ProviderUserId, providerName, l.Title, l.Description, l.Tier, l.HourlyCreditRate, l.IsActive, l.CreatedAt, l.Language);

    public static LedgerEntryDto ToDto(this LedgerEntry e) =>
        new(e.Id, e.Amount, e.EntryType, e.BookingId, e.CreatedAt);
}
