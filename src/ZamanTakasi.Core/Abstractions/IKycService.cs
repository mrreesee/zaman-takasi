namespace ZamanTakasi.Core.Abstractions;

/// <summary>
/// KAPSAM DIŞI — MVP'de İMPLEMENTE EDİLMEZ (yalnızca port/stub).
/// Kimlik doğrulama / KYC sonraki aşamada ele alınacak.
/// </summary>
public interface IKycService
{
    // TODO: KYC sağlayıcı entegrasyonu sonraki aşamada.
    Task<bool> IsVerifiedAsync(Guid userId, CancellationToken ct = default);
}
