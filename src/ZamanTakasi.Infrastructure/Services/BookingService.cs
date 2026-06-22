using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ZamanTakasi.Core.Abstractions;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;
using ZamanTakasi.Core.Services;
using ZamanTakasi.Infrastructure.Persistence;

namespace ZamanTakasi.Infrastructure.Services;

public interface IBookingService
{
    Task<Booking> CreateAsync(Guid requesterUserId, Guid listingId, decimal hours, CancellationToken ct = default);
    Task<Booking> AcceptAsync(Guid bookingId, Guid providerUserId, CancellationToken ct = default);
    Task<Booking> CompleteAsync(Guid bookingId, Guid providerUserId, CancellationToken ct = default);
    Task<Booking> CancelAsync(Guid bookingId, Guid actingUserId, CancellationToken ct = default);
}

public sealed class BookingService : IBookingService
{
    private const int MaxCompletionAttempts = 3;

    private readonly AppDbContext _db;
    private readonly ILedgerService _ledger;
    private readonly IBalanceService _balances;
    private readonly INotificationService _notifications;
    private readonly ILogger<BookingService> _logger;
    private readonly decimal _commissionRate;

    public BookingService(
        AppDbContext db,
        ILedgerService ledger,
        IBalanceService balances,
        INotificationService notifications,
        IOptions<LedgerOptions> options,
        ILogger<BookingService> logger)
    {
        _db = db;
        _ledger = ledger;
        _balances = balances;
        _notifications = notifications;
        _logger = logger;
        _commissionRate = options.Value.CommissionRate;
    }

    public async Task<Booking> CreateAsync(Guid requesterUserId, Guid listingId, decimal hours, CancellationToken ct = default)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == listingId, ct)
            ?? throw new NotFoundException("İlan bulunamadı.");
        if (!listing.IsActive) throw new DomainException("İlan pasif; rezervasyon yapılamaz.");

        // Booking.Create domain kuralı: kendi ilanını rezerve edememe, hours>0, fiyat sabitleme.
        var booking = Booking.Create(listing.Id, requesterUserId, listing.ProviderUserId, hours, listing.Tier);
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        // Pending -> hizmeti verene "yeni rezervasyon talebi" bildirimi.
        await _notifications.SendBookingRequestedAsync(booking.ProviderUserId, booking, ct);
        return booking;
    }

    public async Task<Booking> AcceptAsync(Guid bookingId, Guid providerUserId, CancellationToken ct = default)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.ProviderUserId != providerUserId)
            throw new ForbiddenActionException("Yalnızca hizmeti veren bu rezervasyonu kabul edebilir.");
        booking.Accept();
        await _db.SaveChangesAsync(ct);

        // Accepted -> isteyene "rezervasyonun kabul edildi" bildirimi.
        await _notifications.SendBookingAcceptedAsync(booking.RequesterUserId, booking, ct);
        return booking;
    }

    /// <summary>
    /// İŞ KURALI 2: booking 'Completed' olunca TEK transaction içinde atomik yazılır (ya hepsi ya hiçbiri):
    /// requester -CreditCost, provider +kazanç, platform +komisyon. Toplam her zaman 0.
    ///
    /// EŞZAMANLILIK GÜVENLİĞİ: Bakiye ayrı bir alan değil, LedgerEntry toplamıdır; bu yüzden
    /// "oku-kontrol-et-yaz" adımlarının tamamı IsolationLevel.Serializable bir transaction içinde
    /// yapılır. İki eşzamanlı tamamlama aynı requester'ın bakiyesini birlikte tüketmeye çalışırsa
    /// PostgreSQL SSI bunu serialization_failure (40001) ile reddeder; biz de transaction'ı yeniden
    /// deneriz. Tekrar denemede bakiye taze okunduğu için ikinci işlem yetersiz bakiyeden 400 alır.
    /// Bakiye ASLA negatife düşmez; ayrı bir bakiye alanı YOKTUR.
    /// </summary>
    public async Task<Booking> CompleteAsync(Guid bookingId, Guid providerUserId, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Her denemede temiz başla: önceki başarısız denemenin izlenen değişiklikleri kalmasın.
            _db.ChangeTracker.Clear();
            try
            {
                return await CompleteOnceAsync(bookingId, providerUserId, ct);
            }
            catch (Exception ex) when (IsSerializationConflict(ex) && attempt < MaxCompletionAttempts)
            {
                _logger.LogWarning(
                    "Tamamlama serileştirme çakışması (deneme {Attempt}/{Max}), booking {BookingId}; yeniden deneniyor.",
                    attempt, MaxCompletionAttempts, bookingId);
                await Task.Delay(20 * attempt, ct); // kısa backoff
            }
            catch (Exception ex) when (IsSerializationConflict(ex))
            {
                _logger.LogWarning(
                    "Tamamlama serileştirme çakışması {Max} denemede çözülemedi, booking {BookingId}.",
                    MaxCompletionAttempts, bookingId);
                throw new ConflictException("Eşzamanlı işlem çakışması; lütfen tekrar deneyin.");
            }
        }
    }

    /// <summary>Tek bir serializable tamamlama denemesi. Yetersiz bakiye/geçersiz durum istisna fırlatır.</summary>
    private async Task<Booking> CompleteOnceAsync(Guid bookingId, Guid providerUserId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var booking = await LoadAsync(bookingId, ct);
        if (booking.ProviderUserId != providerUserId)
            throw new ForbiddenActionException("Yalnızca hizmeti veren bu rezervasyonu tamamlayabilir.");

        // Bakiye, transaction'ın İÇİNDE okunur; negatif kontrolü de burada (INSERT'lerden hemen önce).
        var requesterBalance = await _balances.GetBalanceAsync(booking.RequesterUserId, ct);

        // Yetersiz bakiye / geçersiz durum burada istisna fırlatır (DB'ye hiçbir şey yazılmadan).
        var entries = _ledger.BuildCompletionEntries(booking, _commissionRate, requesterBalance);

        booking.Complete();
        _db.LedgerEntries.AddRange(entries);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // İZLENEBİLİRLİK: "kim kime ne kadar aktardı" sorgulanabilir olsun diye ledger hareketini
        // yapılandırılmış (structured) bir olay olarak logla.
        var commission = entries.First(e => e.EntryType == LedgerEntryType.Commission).Amount;
        var earning = entries.First(e => e.EntryType == LedgerEntryType.ServiceEarning).Amount;
        _logger.LogInformation(
            "Ledger hareketi {LedgerEvent}: booking {BookingId} | requester {RequesterId} -> provider {ProviderId} | tutar {Amount} ZK | kazanç {Earning} ZK | komisyon {Commission} ZK",
            "BookingCompletion", booking.Id, booking.RequesterUserId, booking.ProviderUserId, booking.CreditCost, earning, commission);

        // Completed -> isteyene "hizmet tamamlandı" bildirimi (transaction commit'inden SONRA).
        await _notifications.SendBookingCompletedAsync(booking.RequesterUserId, booking, ct);
        return booking;
    }

    /// <summary>
    /// PostgreSQL serialization_failure (40001) veya deadlock (40P01) mı? EF, SaveChanges sırasında
    /// bunu DbUpdateException içine sarabilir; commit sırasında ham PostgresException gelebilir.
    /// </summary>
    private static bool IsSerializationConflict(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is PostgresException pg &&
                (pg.SqlState == PostgresErrorCodes.SerializationFailure ||
                 pg.SqlState == PostgresErrorCodes.DeadlockDetected))
                return true;
        return false;
    }

    public async Task<Booking> CancelAsync(Guid bookingId, Guid actingUserId, CancellationToken ct = default)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.RequesterUserId != actingUserId && booking.ProviderUserId != actingUserId)
            throw new ForbiddenActionException("Yalnızca rezervasyonun tarafları iptal edebilir.");
        booking.Cancel();
        await _db.SaveChangesAsync(ct);
        return booking;
    }

    private async Task<Booking> LoadAsync(Guid bookingId, CancellationToken ct)
        => await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct)
           ?? throw new NotFoundException("Rezervasyon bulunamadı.");
}
