using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Infrastructure.Persistence;

namespace ZamanTakasi.Infrastructure.Services;

public interface IBalanceService
{
    /// <summary>Kullanıcının bakiyesi = kendi LedgerEntry.Amount toplamı (tek doğruluk kaynağı defter).</summary>
    Task<decimal> GetBalanceAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<LedgerEntry>> GetEntriesAsync(Guid userId, CancellationToken ct = default);
}

public sealed class BalanceService : IBalanceService
{
    private readonly AppDbContext _db;
    public BalanceService(AppDbContext db) => _db = db;

    public async Task<decimal> GetBalanceAsync(Guid userId, CancellationToken ct = default)
        => await _db.LedgerEntries.Where(e => e.UserId == userId)
                                  .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

    public async Task<IReadOnlyList<LedgerEntry>> GetEntriesAsync(Guid userId, CancellationToken ct = default)
        => await _db.LedgerEntries.Where(e => e.UserId == userId)
                                  .OrderByDescending(e => e.CreatedAt)
                                  .ToListAsync(ct);
}
