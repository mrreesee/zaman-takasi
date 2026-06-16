using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZamanTakasi.Core.Entities;
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
    private readonly AppDbContext _db;
    private readonly ILedgerService _ledger;
    private readonly IBalanceService _balances;
    private readonly decimal _commissionRate;

    public BookingService(AppDbContext db, ILedgerService ledger, IBalanceService balances, IOptions<LedgerOptions> options)
    {
        _db = db;
        _ledger = ledger;
        _balances = balances;
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
        return booking;
    }

    public async Task<Booking> AcceptAsync(Guid bookingId, Guid providerUserId, CancellationToken ct = default)
    {
        var booking = await LoadAsync(bookingId, ct);
        if (booking.ProviderUserId != providerUserId)
            throw new ForbiddenActionException("Yalnızca hizmeti veren bu rezervasyonu kabul edebilir.");
        booking.Accept();
        await _db.SaveChangesAsync(ct);
        return booking;
    }

    /// <summary>
    /// İŞ KURALI 2: booking 'Completed' olunca TEK transaction içinde atomik yazılır (ya hepsi ya hiçbiri):
    /// requester -CreditCost, provider +kazanç, platform +komisyon. Toplam her zaman 0.
    /// </summary>
    public async Task<Booking> CompleteAsync(Guid bookingId, Guid providerUserId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var booking = await LoadAsync(bookingId, ct);
        if (booking.ProviderUserId != providerUserId)
            throw new ForbiddenActionException("Yalnızca hizmeti veren bu rezervasyonu tamamlayabilir.");

        var requesterBalance = await _balances.GetBalanceAsync(booking.RequesterUserId, ct);

        // Yetersiz bakiye / geçersiz durum burada istisna fırlatır (DB'ye hiçbir şey yazılmadan).
        var entries = _ledger.BuildCompletionEntries(booking, _commissionRate, requesterBalance);

        booking.Complete();
        _db.LedgerEntries.AddRange(entries);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return booking;
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
