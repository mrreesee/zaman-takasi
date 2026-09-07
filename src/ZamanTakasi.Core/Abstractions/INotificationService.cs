using ZamanTakasi.Core.Entities;

namespace ZamanTakasi.Core.Abstractions;

/// <summary>
/// Olaya özgü bildirim gönderimi (e-posta/push). Generic bir SendEmail DEĞİL; her olay kendi metodu.
/// Kapalı betada stub'tur (gerçek gönderim yok). Çağrılar booking durum geçişlerine bağlıdır.
///
/// NOT (mimari): Core'un bağımlılığı olmadığı için parametreler Shared'daki BookingDto değil,
/// Core'un kendi <see cref="Booking"/> domain entity'sidir (Shared -> Core olduğundan ters yön döngü yaratırdı).
///
/// İLERİYE DÖNÜK: Bu arayüz şifre sıfırlama gibi yeni olaylarla, mevcut çağıranları bozmadan
/// yalnızca yeni bir metot eklenerek genişletilebilir.
/// </summary>
public interface INotificationService
{
    /// <summary>Booking Pending olarak oluştuğunda hizmeti verene (provider) bildirim.</summary>
    Task SendBookingRequestedAsync(Guid providerUserId, Booking booking, CancellationToken ct = default);

    /// <summary>Booking Accepted olduğunda hizmeti isteyene (requester) bildirim.</summary>
    Task SendBookingAcceptedAsync(Guid requesterUserId, Booking booking, CancellationToken ct = default);

    /// <summary>Booking Completed olduğunda hizmeti isteyene (requester) bildirim.</summary>
    Task SendBookingCompletedAsync(Guid requesterUserId, Booking booking, CancellationToken ct = default);

    /// <summary>
    /// Kayıt sonrası e-posta doğrulama bağlantısını gönderir. <paramref name="confirmationUrl"/>
    /// API tarafında (App:PublicUrl + token) tam olarak kurulur; gönderici yalnızca iletir.
    /// <paramref name="lang"/> iki harfli arayüz dilidir ("en"/"tr") — e-posta ona göre yazılır.
    /// </summary>
    Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationUrl, string lang, CancellationToken ct = default);

    /// <summary>
    /// Parola sıfırlama bağlantısını gönderir. <paramref name="resetUrl"/> API tarafında (App:PublicUrl + token)
    /// tam olarak kurulur; gönderici yalnızca iletir. <paramref name="lang"/> "en"/"tr".
    /// </summary>
    Task SendPasswordResetAsync(string toEmail, string displayName, string resetUrl, string lang, CancellationToken ct = default);
}
