namespace ZamanTakasi.Infrastructure;

/// <summary>appsettings "Ledger" bölümünden bağlanır.</summary>
public sealed class LedgerOptions
{
    /// <summary>Platform komisyon oranı (varsayılan 0.13).</summary>
    public decimal CommissionRate { get; set; } = 0.13m;

    /// <summary>
    /// Kayıt anında bir kerelik verilen "Hoş Geldin" bakiyesi (varsayılan 3 ZK).
    /// Kapalı devre likidite: OpeningBalance LedgerEntry olarak yazılır; ayrı bakiye alanı YOKTUR.
    /// 0 veya altıysa hiç verilmez.
    /// </summary>
    public decimal WelcomeBalance { get; set; } = 3m;
}
