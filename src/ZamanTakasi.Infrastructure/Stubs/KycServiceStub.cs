using ZamanTakasi.Core.Abstractions;

namespace ZamanTakasi.Infrastructure.Stubs;

/// <summary>KAPSAM DIŞI stub. KYC/kimlik doğrulama MVP'de YOK. // TODO: sonraki aşama.</summary>
public sealed class KycServiceStub : IKycService
{
    public Task<bool> IsVerifiedAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(false); // stub: her zaman "doğrulanmadı"
}
