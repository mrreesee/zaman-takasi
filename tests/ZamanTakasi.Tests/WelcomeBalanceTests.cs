using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using ZamanTakasi.Infrastructure;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Infrastructure.Services;

namespace ZamanTakasi.Tests;

/// <summary>
/// Cold-start güvenliği: kayıt anında verilen Hoş Geldin bakiyesi TAM OLARAK bir kez verilmeli;
/// ikinci veriliş denemesi no-op olmalı (idempotent). Gerçek PostgreSQL ister; Docker yoksa atlanır.
/// </summary>
public sealed class WelcomeBalanceTests : IAsyncLifetime
{
    private PostgreSqlContainer? _pg;
    private string _conn = "";
    private string? _skip;

    public async Task InitializeAsync()
    {
        try
        {
#pragma warning disable CS0618
            _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
#pragma warning restore CS0618
            await _pg.StartAsync();
            _conn = _pg.GetConnectionString();

            await using var db = NewContext();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _skip = "Docker/Postgres kullanılamıyor: " + ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_pg is not null) await _pg.DisposeAsync();
    }

    private AppDbContext NewContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_conn).Options);

    private static WelcomeBalanceService NewService(AppDbContext db, decimal welcome = 3m)
        => new(
            db,
            Options.Create(new LedgerOptions { WelcomeBalance = welcome }),
            NullLogger<WelcomeBalanceService>.Instance);

    [SkippableFact]
    public async Task Welcome_balance_granted_exactly_once()
    {
        Skip.If(_skip is not null, _skip ?? "");

        var userId = Guid.NewGuid();

        await using var ctx = NewContext();
        var service = NewService(ctx, welcome: 3m);

        // İlk veriliş -> true, bakiye tam 3.
        var first = await service.GrantIfFirstTimeAsync(userId);
        Assert.True(first);

        // İkinci deneme -> no-op (false), bakiye değişmez.
        var second = await service.GrantIfFirstTimeAsync(userId);
        Assert.False(second);

        await using var check = NewContext();
        var balance = await check.LedgerEntries
            .Where(e => e.UserId == userId)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;
        Assert.Equal(3m, balance);

        var openingEntryCount = await check.LedgerEntries.CountAsync(e => e.UserId == userId);
        Assert.Equal(1, openingEntryCount); // ikinci veriliş yeni kayıt YAZMADI
    }
}
