using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Infrastructure.Persistence;

namespace ZamanTakasi.Infrastructure.Services;

public interface IWelcomeBalanceService
{
    /// <summary>
    /// Kullanıcıya bir kerelik "Hoş Geldin" bakiyesini verir. İdempotent:
    /// kullanıcının daha önce bir OpeningBalance kaydı varsa hiçbir şey yapmaz.
    /// </summary>
    /// <returns>true = bu çağrıda verildi; false = zaten vardı / kapalı.</returns>
    Task<bool> GrantIfFirstTimeAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Cold-start (tavuk-yumurta) çözümü: yeni kullanıcıya kayıt anında likidite enjekte eder.
/// Bunu MEVCUT mekanizmayla yapar — tek bir <see cref="LedgerEntryType.OpeningBalance"/> LedgerEntry
/// yazarak (demo seed'deki açılış bakiyesiyle aynı yöntem). AYRI bir bakiye alanı oluşturmaz.
///
/// DEĞİŞMEZ NOTU: Welcome/Opening kredileri tasarım gereği sisteme enjekte edilen likiditedir;
/// bunlar sistem GENELİNDE sıfır toplamaz ve bu doğrudur. Yalnızca booking başına 3-kayıt kuralı
/// sıfır toplar. Bu yüzden burada sistem-geneli sıfır-toplam kontrolü YOKTUR.
/// </summary>
public sealed class WelcomeBalanceService : IWelcomeBalanceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<WelcomeBalanceService> _logger;
    private readonly decimal _amount;

    public WelcomeBalanceService(AppDbContext db, IOptions<LedgerOptions> options, ILogger<WelcomeBalanceService> logger)
    {
        _db = db;
        _logger = logger;
        _amount = options.Value.WelcomeBalance;
    }

    public async Task<bool> GrantIfFirstTimeAsync(Guid userId, CancellationToken ct = default)
    {
        if (_amount <= 0m) return false; // yapılandırma ile kapatılmış

        // İdempotensi: KYC stub olduğu için çoklu hesap riski var; en azından aynı kullanıcıya
        // ikinci kez welcome verilmesini engelle.
        var alreadyGranted = await _db.LedgerEntries
            .AnyAsync(e => e.UserId == userId && e.EntryType == LedgerEntryType.OpeningBalance, ct);
        if (alreadyGranted)
        {
            _logger.LogWarning("Hoş Geldin bakiyesi atlandı (zaten OpeningBalance var): kullanıcı {UserId}", userId);
            return false;
        }

        var entry = new LedgerEntry(userId, _amount, LedgerEntryType.OpeningBalance);
        _db.LedgerEntries.Add(entry);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Yarış: iki eşzamanlı onay aynı anda "henüz yok" gördü. DB'deki kısmi tekil indeks
            // (UserId WHERE EntryType = OpeningBalance) ikinciyi reddeder; sessizce no-op.
            _db.Entry(entry).State = EntityState.Detached;
            _logger.LogWarning("Hoş Geldin bakiyesi yarışta atlandı (tekil indeks): kullanıcı {UserId}", userId);
            return false;
        }

        // Suistimal farkındalığı: her veriliş userId + miktar ile structured loglanır.
        _logger.LogInformation("Hoş Geldin bakiyesi verildi: kullanıcı {UserId}, miktar {Amount} ZK", userId, _amount);
        return true;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is Npgsql.PostgresException pg && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
                return true;
        return false;
    }
}
