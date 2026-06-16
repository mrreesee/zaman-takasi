using ZamanTakasi.Core.Abstractions;

namespace ZamanTakasi.Infrastructure.Stubs;

/// <summary>
/// KAPSAM DIŞI stub. Nakitle kredi satışı MVP'de YOK (e-para/ödeme riski).
/// Krediler kapalı devre: TL'ye çevrilemez. Çağrılırsa kasıtlı olarak hata verir.
/// </summary>
public sealed class PaymentServiceStub : IPaymentService
{
    public Task PurchaseCreditsAsync(Guid userId, decimal credits, CancellationToken ct = default)
        => throw new NotImplementedException("KAPSAM DIŞI: nakitle kredi satışı MVP'de yok. // TODO: sonraki aşama.");
}
