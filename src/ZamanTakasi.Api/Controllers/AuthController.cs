using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZamanTakasi.Api.Auth;
using ZamanTakasi.Core.Abstractions;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Infrastructure.Identity;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Infrastructure.Services;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")] // uç geneli sabit pencere (Program.cs) — Resend kotası ve kaba kuvvete karşı üst sınır
public sealed class AuthController : ControllerBase
{
    // E-posta bazlı eşikler (AuthThrottle). IP kullanılmaz: Web → API çağrıları aynı iç adresten gelir.
    private const int LoginAttemptsPerWindow = 20;          // hesap kilidi (5 hatalı) asıl koruma; bu, üst sınır
    private const int RegistrationsPerWindow = 30;          // uç geneli, 10 dk
    private const int ResendPerEmailPerWindow = 3;          // aynı e-postaya 15 dk'da en fazla 3 onay maili
    private const int ResendGlobalPerWindow = 40;           // uç geneli, 1 saat (Resend ücretsiz kota: 100/gün)
    private static readonly TimeSpan ShortWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);
    private const string TooManyMessage = "Çok fazla deneme. Lütfen biraz sonra tekrar dene.";

    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IWelcomeBalanceService _welcome;
    private readonly INotificationService _notifications;
    private readonly AuthOptions _authOptions;
    private readonly IConfiguration _config;
    private readonly AuthThrottle _throttle;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        JwtTokenService jwt,
        IWelcomeBalanceService welcome,
        INotificationService notifications,
        IOptions<AuthOptions> authOptions,
        IConfiguration config,
        AuthThrottle throttle,
        ILogger<AuthController> logger)
    {
        _users = users;
        _db = db;
        _jwt = jwt;
        _welcome = welcome;
        _notifications = notifications;
        _authOptions = authOptions.Value;
        _config = config;
        _throttle = throttle;
        _logger = logger;
    }

    /// <summary>Yeni kullanıcı: ApplicationUser (kimlik) + domain User (profil) AYNI transaction'da, AYNI Guid Id ile.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest("Email, Password ve DisplayName zorunlu.");
        if (req.DisplayName.Trim().Length > ZamanTakasi.Core.Entities.User.DisplayNameMaxLength)
            return BadRequest($"Görünen ad en fazla {ZamanTakasi.Core.Entities.User.DisplayNameMaxLength} karakter olabilir.");

        if (!_throttle.TryAcquire("register:global", RegistrationsPerWindow, ShortWindow))
        {
            _logger.LogWarning("Kayıt eşiği aşıldı (uç geneli).");
            return StatusCode(StatusCodes.Status429TooManyRequests, TooManyMessage);
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var appUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = req.Email, Email = req.Email };
        var result = await _users.CreateAsync(appUser, req.Password);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        var domainUser = new User(appUser.Id, req.DisplayName);
        _db.DomainUsers.Add(domainUser);
        await _db.SaveChangesAsync();

        // KATI KAPI (AuthOptions.RequireEmailConfirmation): Hoş Geldin bakiyesi ve giriş token'ı VERİLMEZ.
        // Kullanıcı e-postasını doğrulayana kadar bekler; bakiye onayda verilir (suistimal önleme).
        if (_authOptions.RequireEmailConfirmation)
        {
            await tx.CommitAsync(); // kullanıcı oluştu; e-posta gönderimi DB transaction'ı DIŞINDA
            await SendConfirmationEmailAsync(appUser, domainUser.DisplayName, req.Lang);
            return Ok(new RegisterResponse(RequiresEmailConfirmation: true, Auth: null));
        }

        // KAPI KAPALI (varsayılan): mevcut davranış — kayıt anında bir kerelik Hoş Geldin bakiyesi + anında giriş.
        await _welcome.GrantIfFirstTimeAsync(appUser.Id);
        await tx.CommitAsync();

        var (token, exp) = _jwt.Create(appUser.Id, req.Email, domainUser.DisplayName);
        return Ok(new RegisterResponse(RequiresEmailConfirmation: false,
            Auth: new AuthResponse(token, appUser.Id, domainUser.DisplayName, exp)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized("E-posta veya parola hatalı.");

        if (!_throttle.TryAcquire(AuthThrottle.EmailKey("login", req.Email), LoginAttemptsPerWindow, ShortWindow))
            return StatusCode(StatusCodes.Status429TooManyRequests, TooManyMessage);

        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null)
            return Unauthorized("E-posta veya parola hatalı.");

        // Hesap kilidi: CheckPasswordAsync kilidi kendiliğinden uygulamaz; burada elle uygulanır.
        if (await _users.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Kilitli hesaba giriş denemesi: kullanıcı {UserId}", user.Id);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                "Çok fazla hatalı deneme; hesap geçici olarak kilitlendi. 15 dakika sonra tekrar dene.");
        }

        if (!await _users.CheckPasswordAsync(user, req.Password))
        {
            await _users.AccessFailedAsync(user); // sayaç; eşikte kilit (Lockout seçenekleri)
            return Unauthorized("E-posta veya parola hatalı.");
        }

        if (user.AccessFailedCount > 0)
            await _users.ResetAccessFailedCountAsync(user);

        // Katı kapı açıkken doğrulanmamış e-posta ile giriş engellenir (403 -> Web "tekrar gönder" sunar).
        if (_authOptions.RequireEmailConfirmation && !user.EmailConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden, "E-posta adresin henüz doğrulanmadı.");

        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
        var name = domainUser?.DisplayName ?? user.Email!;
        var (token, exp) = _jwt.Create(user.Id, user.Email!, name);
        return Ok(new AuthResponse(token, user.Id, name, exp));
    }

    /// <summary>E-posta doğrulama bağlantısındaki userId+token ile onay. Başarılıysa Hoş Geldin bakiyesi verilir ve giriş token'ı döner.</summary>
    [HttpPost("confirm-email")]
    public async Task<ActionResult<AuthResponse>> ConfirmEmail(ConfirmEmailRequest req)
    {
        var user = await _users.FindByIdAsync(req.UserId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(req.Token))
            return BadRequest("Geçersiz doğrulama bağlantısı.");

        string decodedToken;
        try { decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token)); }
        catch { return BadRequest("Geçersiz doğrulama bağlantısı."); }

        // GÜVENLİK: Token HER DURUMDA doğrulanır — kullanıcı zaten onaylı olsa bile. Aksi halde
        // userId (ilanlarda herkese açık) + rastgele token ile başkasının hesabına giriş yapılabilirdi.
        if (user.EmailConfirmed)
        {
            // Linke ikinci tıklama: token hâlâ geçerliyse (ömrü dolmadıysa) hata verme, giriş sağla.
            var stillValid = await _users.VerifyUserTokenAsync(
                user, _users.Options.Tokens.EmailConfirmationTokenProvider, UserManager<ApplicationUser>.ConfirmEmailTokenPurpose, decodedToken);
            if (!stillValid)
                return BadRequest("Doğrulama bağlantısı geçersiz veya süresi dolmuş. Giriş yapmayı dene.");
            return await IssueForConfirmedAsync(user);
        }

        var result = await _users.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
            return BadRequest("Doğrulama bağlantısı geçersiz veya süresi dolmuş. Yeni bir bağlantı iste.");

        // Onay başarılı: Hoş Geldin bakiyesini ŞİMDİ ver (idempotent) ve kullanıcıyı giriş yapmış say.
        await _welcome.GrantIfFirstTimeAsync(user.Id);
        return await IssueForConfirmedAsync(user);
    }

    /// <summary>Doğrulama e-postasını yeniden gönderir. Hesap varlığını sızdırmamak için cevap her zaman 200.</summary>
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            // Eşikler: aynı e-postaya kısa sürede tekrar tekrar mail atılmasın; uç geneli Resend kotası korunsun.
            // Aşımda da 200 döner (hesap varlığı sızmasın) — yalnızca gönderim atlanır ve loglanır.
            if (!_throttle.TryAcquire(AuthThrottle.EmailKey("resend", req.Email), ResendPerEmailPerWindow, ResendWindow)
                || !_throttle.TryAcquire("resend:global", ResendGlobalPerWindow, HourWindow))
            {
                _logger.LogWarning("Onay maili tekrar gönderme eşiği aşıldı; gönderim atlandı.");
                return Ok();
            }

            var user = await _users.FindByEmailAsync(req.Email);
            if (user is not null && !user.EmailConfirmed)
            {
                var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
                await SendConfirmationEmailAsync(user, domainUser?.DisplayName ?? user.Email!, req.Lang);
            }
        }
        return Ok(); // her durumda 200
    }

    /// <summary>"Parolamı unuttum": bağlantı e-postayla gider. Hesap varlığını sızdırmamak için cevap her zaman 200.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) return Ok();

        if (!_throttle.TryAcquire(AuthThrottle.EmailKey("pwreset", req.Email), ResendPerEmailPerWindow, ResendWindow)
            || !_throttle.TryAcquire("pwreset:global", ResendGlobalPerWindow, HourWindow))
        {
            _logger.LogWarning("Parola sıfırlama eşiği aşıldı; gönderim atlandı.");
            return Ok();
        }

        var user = await _users.FindByEmailAsync(req.Email);
        if (user is not null)
        {
            var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
            var rawToken = await _users.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
            var publicUrl = (_config["App:PublicUrl"] ?? "").TrimEnd('/');
            var url = $"{publicUrl}/reset-password?userId={user.Id}&token={encoded}&lang={Uri.EscapeDataString(req.Lang)}";
            await _notifications.SendPasswordResetAsync(user.Email!, domainUser?.DisplayName ?? user.Email!, url, req.Lang);
        }
        return Ok(); // her durumda 200
    }

    /// <summary>Sıfırlama bağlantısındaki token ile yeni parola. Başarıda kilit/hatalı sayaç sıfırlanır; token tek kullanımlıktır.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Geçersiz sıfırlama bağlantısı.");

        var user = await _users.FindByIdAsync(req.UserId.ToString());
        if (user is null) return BadRequest("Geçersiz sıfırlama bağlantısı.");

        string decodedToken;
        try { decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token)); }
        catch { return BadRequest("Geçersiz sıfırlama bağlantısı."); }

        var result = await _users.ResetPasswordAsync(user, decodedToken, req.NewPassword);
        if (!result.Succeeded)
        {
            // Token hatası ile parola politikası hatasını ayır: politika mesajı kullanıcıya faydalı, token mesajı tek tip.
            var policyErrors = result.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).ToList();
            if (policyErrors.Count > 0)
                return BadRequest(string.Join("; ", policyErrors.Select(e => e.Description)));
            return BadRequest("Sıfırlama bağlantısı geçersiz veya süresi dolmuş. Yeni bir bağlantı iste.");
        }

        // Parola e-posta üzerinden sıfırlandı => e-posta sahipliği kanıtlandı. Kilidi ve hatalı sayacı temizle.
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _users.UpdateAsync(user);
            await _welcome.GrantIfFirstTimeAsync(user.Id);
        }

        _logger.LogInformation("Parola sıfırlandı: kullanıcı {UserId}", user.Id);
        return Ok();
    }

    private async Task<ActionResult<AuthResponse>> IssueForConfirmedAsync(ApplicationUser user)
    {
        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
        var name = domainUser?.DisplayName ?? user.Email!;
        var (token, exp) = _jwt.Create(user.Id, user.Email!, name);
        return Ok(new AuthResponse(token, user.Id, name, exp));
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, string displayName, string lang)
    {
        var rawToken = await _users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        // Bağlantı WEB uygulamasına gitmeli (App:PublicUrl = https://zamantakas.com). API private olduğu için burada zorunlu.
        var publicUrl = (_config["App:PublicUrl"] ?? "").TrimEnd('/');
        var url = $"{publicUrl}/confirm-email?userId={user.Id}&token={encoded}&lang={Uri.EscapeDataString(lang)}";

        await _notifications.SendEmailConfirmationAsync(user.Email!, displayName, url, lang);
    }
}
