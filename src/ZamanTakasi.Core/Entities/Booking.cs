using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;
using ZamanTakasi.Core.Services;

namespace ZamanTakasi.Core.Entities;

public class Booking
{
    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid RequesterUserId { get; private set; }
    public Guid ProviderUserId { get; private set; }
    public decimal Hours { get; private set; }

    /// <summary>Booking oluşturulurken SABİTLENİR: Hours * (int)Tier (2 ondalık).</summary>
    public decimal CreditCost { get; private set; }

    public BookingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private Booking() { } // EF Core

    private Booking(Guid listingId, Guid requesterUserId, Guid providerUserId, decimal hours, SkillTier tier)
    {
        if (listingId == Guid.Empty) throw new DomainException("ListingId zorunlu.");
        if (hours <= 0) throw new DomainException("Hours 0'dan büyük olmalı.");
        if (requesterUserId == providerUserId) throw new DomainException("Kendi ilanını rezerve edemezsin.");

        Id = Guid.NewGuid();
        ListingId = listingId;
        RequesterUserId = requesterUserId;
        ProviderUserId = providerUserId;
        Hours = LedgerMath.Round2(hours);
        CreditCost = LedgerMath.Round2(Hours * (int)tier); // fiyat burada sabitlenir
        Status = BookingStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static Booking Create(Guid listingId, Guid requesterUserId, Guid providerUserId, decimal hours, SkillTier tier)
        => new(listingId, requesterUserId, providerUserId, hours, tier);

    public void Accept()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidBookingTransitionException($"Yalnızca Pending booking kabul edilebilir (mevcut: {Status}).");
        Status = BookingStatus.Accepted;
    }

    /// <summary>Durumu Completed yapar. Defter kayıtları ledger servisinden üretilip aynı transaction'da yazılır.</summary>
    public void Complete()
    {
        if (Status != BookingStatus.Accepted)
            throw new InvalidBookingTransitionException($"Yalnızca Accepted booking tamamlanabilir (mevcut: {Status}).");
        Status = BookingStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is not (BookingStatus.Pending or BookingStatus.Accepted))
            throw new InvalidBookingTransitionException($"Yalnızca Pending/Accepted booking iptal edilebilir (mevcut: {Status}).");
        Status = BookingStatus.Cancelled;
    }
}
