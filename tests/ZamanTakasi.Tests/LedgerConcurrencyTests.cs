using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;
using ZamanTakasi.Core.Services;
using ZamanTakasi.Infrastructure;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Infrastructure.Services;
using ZamanTakasi.Infrastructure.Stubs;

namespace ZamanTakasi.Tests;

/// <summary>
/// EN KRİTİK GÜVENLİK TESTİ: bakiye = LedgerEntry toplamı olduğu için, iki eşzamanlı tamamlama
/// aynı requester'ın bakiyesini birlikte tüketmeye çalışırsa bakiye negatife düşmemeli ve
/// TAM OLARAK biri başarılı olmalı. Gerçek PostgreSQL (serializable) gerekir; bu yüzden
/// Testcontainers ile geçici bir Postgres ayağa kaldırılır. Docker yoksa test atlanır.
/// </summary>
public sealed class LedgerConcurrencyTests : IAsyncLifetime
{
    private PostgreSqlContainer? _pg;
    private string _conn = "";
    private string? _skip;

    public async Task InitializeAsync()
    {
        try
        {
#pragma warning disable CS0618 // parametresiz ctor obsolete; image'i WithImage ile pinliyoruz
            _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
#pragma warning restore CS0618
            await _pg.StartAsync();
            _conn = _pg.GetConnectionString();

            await using var db = NewContext();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            // Docker yok/çalışmıyor -> testleri atla (suite yeşil kalır).
            _skip = "Docker/Postgres kullanılamıyor: " + ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_pg is not null) await _pg.DisposeAsync();
    }

    private AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_conn).Options);

    private static BookingService NewService(AppDbContext db)
        => new(
            db,
            new LedgerService(),
            new BalanceService(db),
            new NotificationServiceStub(NullLogger<NotificationServiceStub>.Instance),
            Options.Create(new LedgerOptions { CommissionRate = 0.13m }),
            NullLogger<BookingService>.Instance);

    [SkippableFact]
    public async Task Concurrent_completions_never_overdraw_balance()
    {
        Skip.If(_skip is not null, _skip ?? "");

        var requester = Guid.NewGuid();
        var provider = Guid.NewGuid();
        Guid b1, b2;

        // Açılış bakiyesi YALNIZCA tek bir tamamlamaya yeter (2 ZK). İki accepted booking, her biri 2 ZK.
        await using (var seed = NewContext())
        {
            seed.LedgerEntries.Add(new LedgerEntry(requester, 2m, LedgerEntryType.OpeningBalance));

            var booking1 = Booking.Create(Guid.NewGuid(), requester, provider, 1m, SkillTier.Verified); // cost = 2
            var booking2 = Booking.Create(Guid.NewGuid(), requester, provider, 1m, SkillTier.Verified); // cost = 2
            booking1.Accept();
            booking2.Accept();
            seed.Bookings.AddRange(booking1, booking2);
            await seed.SaveChangesAsync();
            b1 = booking1.Id;
            b2 = booking2.Id;
        }

        // İki tamamlamayı AYRI context'lerle (DbContext thread-safe değil) eşzamanlı tetikle.
        await using var ctx1 = NewContext();
        await using var ctx2 = NewContext();
        var svc1 = NewService(ctx1);
        var svc2 = NewService(ctx2);

        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => svc1.CompleteAsync(b1, provider)),
            Record.ExceptionAsync(() => svc2.CompleteAsync(b2, provider)));

        var successCount = errors.Count(e => e is null);
        var failures = errors.Where(e => e is not null).ToList();

        // TAM OLARAK biri başarılı, biri başarısız.
        Assert.Equal(1, successCount);
        Assert.Single(failures);
        // Kaybeden ya yetersiz bakiye (400) ya da çözülemeyen çakışma (409) alır — ikisi de DomainException.
        Assert.IsAssignableFrom<DomainException>(failures[0]);

        // Bakiye ASLA negatife düşmez; tam bir tamamlama sonrası 2 - 2 = 0.
        await using var check = NewContext();
        var balance = await check.LedgerEntries
            .Where(e => e.UserId == requester)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;
        Assert.True(balance >= 0m, $"Bakiye negatife düştü: {balance}");
        Assert.Equal(0m, balance);

        // Yalnızca bir booking Completed olmalı.
        var completed = await check.Bookings.CountAsync(x => x.Status == BookingStatus.Completed);
        Assert.Equal(1, completed);
    }

    [SkippableFact]
    public async Task Single_completion_writes_three_entries_summing_zero()
    {
        Skip.If(_skip is not null, _skip ?? "");

        var requester = Guid.NewGuid();
        var provider = Guid.NewGuid();
        Guid bookingId;

        await using (var seed = NewContext())
        {
            seed.LedgerEntries.Add(new LedgerEntry(requester, 10m, LedgerEntryType.OpeningBalance));
            var booking = Booking.Create(Guid.NewGuid(), requester, provider, 1m, SkillTier.Professional); // cost = 3
            booking.Accept();
            seed.Bookings.Add(booking);
            await seed.SaveChangesAsync();
            bookingId = booking.Id;
        }

        await using var ctx = NewContext();
        var result = await NewService(ctx).CompleteAsync(bookingId, provider);
        Assert.Equal(BookingStatus.Completed, result.Status);

        await using var check = NewContext();
        var bookingEntries = await check.LedgerEntries.Where(e => e.BookingId == bookingId).ToListAsync();
        Assert.Equal(3, bookingEntries.Count);
        Assert.Equal(0m, bookingEntries.Sum(e => e.Amount)); // 3-kayıt-sıfır-toplam değişmezi
        Assert.Equal(7m, await check.LedgerEntries.Where(e => e.UserId == requester).SumAsync(e => e.Amount)); // 10 - 3
    }
}
