using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;

namespace ZamanTakasi.Core.Services;

public interface ILedgerService
{
    /// <summary>
    /// Bir booking 'Completed' olurken yazılacak ÇİFT KAYITLI defter kayıtlarını üretir.
    /// Üç kaydın Amount toplamı HER ZAMAN tam 0'dır. requesterBalance defterden gelir;
    /// yetersizse <see cref="InsufficientBalanceException"/> fırlatılır.
    /// </summary>
    IReadOnlyList<LedgerEntry> BuildCompletionEntries(Booking booking, decimal commissionRate, decimal requesterBalance);
}

public sealed class LedgerService : ILedgerService
{
    public IReadOnlyList<LedgerEntry> BuildCompletionEntries(Booking booking, decimal commissionRate, decimal requesterBalance)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (booking.Status != BookingStatus.Accepted)
            throw new InvalidBookingTransitionException($"Tamamlama yalnızca Accepted booking için (mevcut: {booking.Status}).");
        if (commissionRate < 0m || commissionRate >= 1m)
            throw new DomainException("Komisyon oranı [0,1) aralığında olmalı.");

        var cost = booking.CreditCost;

        // İŞ KURALI 4: yetersiz bakiyede negatife düşülemez (Zaman Avansı sonraki aşama).
        if (requesterBalance < cost)
            throw new InsufficientBalanceException(
                $"Yetersiz bakiye. Gerekli: {cost} ZK, mevcut: {requesterBalance} ZK.");

        // Komisyonu yuvarla, kazancı KALAN olarak hesapla => üç kaydın toplamı HER ZAMAN tam 0.
        var commission = LedgerMath.Round2(cost * commissionRate);
        var earning = cost - commission;

        return new[]
        {
            new LedgerEntry(booking.RequesterUserId, -cost, LedgerEntryType.ServicePayment, booking.Id),
            new LedgerEntry(booking.ProviderUserId, earning, LedgerEntryType.ServiceEarning, booking.Id),
            new LedgerEntry(SystemAccounts.PlatformUserId, commission, LedgerEntryType.Commission, booking.Id),
        };
    }
}
