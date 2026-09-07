using Microsoft.Extensions.Logging;
using ZamanTakasi.Core.Abstractions;
using ZamanTakasi.Core.Entities;

namespace ZamanTakasi.Infrastructure.Stubs;

/// <summary>
/// KAPSAM DIŞI stub: gerçek e-posta/push GÖNDERMEZ. Bunun yerine "gönderiyormuş gibi"
/// yapılandırılmış (structured) bir log yazar — kim, hangi olay, hangi booking sorgulanabilir olsun.
/// Adım 5'te Serilog devreye girince bu loglar otomatik olarak Serilog sink'lerine akar
/// (ILogger soyutlaması değişmediği için burada refactor gerekmez).
/// </summary>
public sealed class NotificationServiceStub : INotificationService
{
    private readonly ILogger<NotificationServiceStub> _logger;

    public NotificationServiceStub(ILogger<NotificationServiceStub> logger) => _logger = logger;

    public Task SendBookingRequestedAsync(Guid providerUserId, Booking booking, CancellationToken ct = default)
        => LogPretend("BookingRequested", providerUserId, booking);

    public Task SendBookingAcceptedAsync(Guid requesterUserId, Booking booking, CancellationToken ct = default)
        => LogPretend("BookingAccepted", requesterUserId, booking);

    public Task SendBookingCompletedAsync(Guid requesterUserId, Booking booking, CancellationToken ct = default)
        => LogPretend("BookingCompleted", requesterUserId, booking);

    public Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationUrl, string lang, CancellationToken ct = default)
    {
        // Stub: gerçek e-posta göndermez. Doğrulama bağlantısını loglar ki yerelde (Resend'siz) akış test edilebilsin.
        _logger.LogInformation(
            "Bildirim (stub) e-posta doğrulama: alıcı {Email} | dil {Lang} | bağlantı {ConfirmationUrl}",
            toEmail, lang, confirmationUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, string lang, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Bildirim (stub) parola sıfırlama: alıcı {Email} | dil {Lang} | bağlantı {ResetUrl}",
            toEmail, lang, resetUrl);
        return Task.CompletedTask;
    }

    private Task LogPretend(string notification, Guid recipientUserId, Booking booking)
    {
        _logger.LogInformation(
            "Bildirim (stub) gönderiliyormuş gibi: {Notification} -> alıcı {RecipientUserId} | booking {BookingId} | durum {Status} | {Hours} saat | {CreditCost} ZK",
            notification, recipientUserId, booking.Id, booking.Status, booking.Hours, booking.CreditCost);
        return Task.CompletedTask;
    }
}
