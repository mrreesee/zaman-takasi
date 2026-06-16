namespace ZamanTakasi.Infrastructure;

/// <summary>appsettings "Ledger" bölümünden bağlanır. Komisyon varsayılanı 0.13.</summary>
public sealed class LedgerOptions
{
    public decimal CommissionRate { get; set; } = 0.13m;
}
