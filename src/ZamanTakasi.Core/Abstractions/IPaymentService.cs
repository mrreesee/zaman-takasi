namespace ZamanTakasi.Core.Abstractions;

/// <summary>
/// KAPSAM DIŞI — MVP'de İMPLEMENTE EDİLMEZ (yalnızca port/stub).
/// Nakitle kredi satışı e-para/ödeme riski taşır; ayrı hukuki + teknik aşamada ele alınacak.
/// Krediler bu MVP'de KAPALI DEVRE: hiçbir akış krediyi TL'ye/nakde çeviremez.
/// </summary>
public interface IPaymentService
{
    // TODO: Ödeme sağlayıcı entegrasyonu sonraki aşamada; imza ileride netleşecek.
    Task PurchaseCreditsAsync(Guid userId, decimal credits, CancellationToken ct = default);
}
