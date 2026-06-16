using ZamanTakasi.Core.Enums;

namespace ZamanTakasi.Core.Entities;

/// <summary>
/// DEĞİŞMEZ (immutable) defter kaydı. Bir kez yazılır; ASLA güncellenmez/silinmez.
/// Amount: + alacak, - borç. Kullanıcının bakiyesi = kendi LedgerEntry.Amount toplamı.
/// </summary>
public class LedgerEntry
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? BookingId { get; private set; }
    public decimal Amount { get; private set; }
    public LedgerEntryType EntryType { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LedgerEntry() { } // EF Core

    public LedgerEntry(Guid userId, decimal amount, LedgerEntryType entryType, Guid? bookingId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Amount = amount;
        EntryType = entryType;
        BookingId = bookingId;
        CreatedAt = DateTime.UtcNow;
    }
}
