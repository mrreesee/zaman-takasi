using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZamanTakasi.Core.Abstractions;
using ZamanTakasi.Core.Entities;

namespace ZamanTakasi.Infrastructure.Services;

/// <summary>Resend yapılandırması. ApiKey env'den (Resend__ApiKey) gelir; boşsa gerçek gönderim YAPILMAZ (log fallback).</summary>
public sealed class ResendOptions
{
    public string? ApiKey { get; set; }
    /// <summary>Gönderen adresi. Doğrulanmış domain gerekir (ör. noreply@zamantakas.com).</summary>
    public string From { get; set; } = "noreply@zamantakas.com";
}

/// <summary>Kimlik/kayıt kapısı yapılandırması.</summary>
public sealed class AuthOptions
{
    /// <summary>
    /// true: e-posta doğrulanmadan giriş yok, Hoş Geldin bakiyesi yalnızca onayda verilir (katı kapı).
    /// false (varsayılan): mevcut davranış — kayıtta anında giriş + Hoş Geldin bakiyesi.
    /// Resend kurulup env'ler girilene kadar KAPALI tutulur ki canlı akış bozulmasın.
    /// </summary>
    public bool RequireEmailConfirmation { get; set; }
}

/// <summary>
/// INotificationService'in gerçek implementasyonu. E-posta doğrulama mailini <b>Resend</b> ile gönderir.
/// Resend ApiKey yapılandırılmamışsa (yerel geliştirme) gerçek gönderim yerine bağlantıyı <b>loglar</b> —
/// böylece akış Resend olmadan da test edilebilir. Booking bildirimleri şimdilik (stub gibi) loglanır;
/// gerçek booking e-postaları sonraki iş.
/// </summary>
public sealed class ResendNotificationService : INotificationService
{
    private const string ResendEndpoint = "https://api.resend.com/emails";

    private readonly HttpClient _http;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendNotificationService> _logger;

    public ResendNotificationService(HttpClient http, IOptions<ResendOptions> options, ILogger<ResendNotificationService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string displayName, string confirmationUrl, string lang, CancellationToken ct = default)
    {
        var (subject, html) = BuildConfirmationEmail(displayName, confirmationUrl, lang);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            // Resend kapalı: yerelde/henüz kurulmadıysa bağlantıyı logla (gerçek gönderim yok).
            _logger.LogWarning(
                "Resend ApiKey yok — e-posta GÖNDERİLMEDİ. Doğrulama bağlantısı (dev): {Email} -> {ConfirmationUrl}",
                toEmail, confirmationUrl);
            return;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, ResendEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        req.Content = JsonContent.Create(new
        {
            from = _options.From,
            to = new[] { toEmail },
            subject,
            html
        });

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            // Gönderim başarısız: kayıt akışını çökertme — logla, kullanıcı "tekrar gönder" ile deneyebilir.
            _logger.LogError(
                "Resend e-posta gönderimi başarısız ({Status}) alıcı {Email}: {Body}",
                (int)res.StatusCode, toEmail, body);
            return;
        }

        _logger.LogInformation("E-posta doğrulama gönderildi (Resend): {Email} | dil {Lang}", toEmail, lang);
    }

    // ---- Booking bildirimleri: şimdilik yalnızca structured log (gerçek e-posta sonraki iş) ----
    public Task SendBookingRequestedAsync(Guid providerUserId, Booking booking, CancellationToken ct = default)
        => LogBooking("BookingRequested", providerUserId, booking);

    public Task SendBookingAcceptedAsync(Guid requesterUserId, Booking booking, CancellationToken ct = default)
        => LogBooking("BookingAccepted", requesterUserId, booking);

    public Task SendBookingCompletedAsync(Guid requesterUserId, Booking booking, CancellationToken ct = default)
        => LogBooking("BookingCompleted", requesterUserId, booking);

    private Task LogBooking(string notification, Guid recipientUserId, Booking booking)
    {
        _logger.LogInformation(
            "Bildirim {Notification} -> alıcı {RecipientUserId} | booking {BookingId} | durum {Status}",
            notification, recipientUserId, booking.Id, booking.Status);
        return Task.CompletedTask;
    }

    /// <summary>İki dilli (EN/TR) doğrulama e-postası. Basit, inline-stilli HTML — e-posta istemcileri için güvenli.</summary>
    private static (string Subject, string Html) BuildConfirmationEmail(string displayName, string url, string lang)
    {
        var name = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(displayName) ? "" : displayName);
        var safeUrl = System.Net.WebUtility.HtmlEncode(url);
        var tr = string.Equals(lang, "tr", StringComparison.OrdinalIgnoreCase);

        var subject = tr ? "E-postanı doğrula — Zaman Takası" : "Confirm your email — Zaman Takası";
        var greeting = tr ? $"Merhaba {name}," : $"Hi {name},";
        var intro = tr
            ? "Zaman Takası'na hoş geldin! Hesabını etkinleştirmek ve 3 ZK hoş geldin bakiyeni almak için e-postanı doğrula."
            : "Welcome to Zaman Takası! Confirm your email to activate your account and receive your 3 ZK welcome balance.";
        var button = tr ? "E-postamı doğrula" : "Confirm my email";
        var fallback = tr
            ? "Buton çalışmazsa bu bağlantıyı tarayıcına yapıştır:"
            : "If the button doesn't work, paste this link into your browser:";
        var ignore = tr
            ? "Bu hesabı sen oluşturmadıysan bu e-postayı yok sayabilirsin."
            : "If you didn't create this account, you can safely ignore this email.";

        var html = $@"<div style=""font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0f172a"">
  <div style=""font-size:20px;font-weight:700;color:#0ea5b7;margin-bottom:16px"">Zaman Takası</div>
  <p style=""font-size:15px;margin:0 0 8px"">{greeting}</p>
  <p style=""font-size:15px;line-height:1.5;margin:0 0 20px;color:#334155"">{intro}</p>
  <a href=""{safeUrl}"" style=""display:inline-block;background:#0ea5b7;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:8px;font-size:15px"">{button}</a>
  <p style=""font-size:12.5px;color:#64748b;margin:22px 0 6px"">{fallback}</p>
  <p style=""font-size:12.5px;word-break:break-all;margin:0 0 20px""><a href=""{safeUrl}"" style=""color:#0ea5b7"">{safeUrl}</a></p>
  <hr style=""border:none;border-top:1px solid #e2e8f0;margin:20px 0"" />
  <p style=""font-size:12px;color:#94a3b8;margin:0"">{ignore}</p>
</div>";

        return (subject, html);
    }
}
