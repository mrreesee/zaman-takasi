using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IWelcomeBalanceService _welcome;
    private readonly INotificationService _notifications;
    private readonly AuthOptions _authOptions;
    private readonly IConfiguration _config;

    public AuthController(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        JwtTokenService jwt,
        IWelcomeBalanceService welcome,
        INotificationService notifications,
        IOptions<AuthOptions> authOptions,
        IConfiguration config)
    {
        _users = users;
        _db = db;
        _jwt = jwt;
        _welcome = welcome;
        _notifications = notifications;
        _authOptions = authOptions.Value;
        _config = config;
    }

    /// <summary>Yeni kullanıcı: ApplicationUser (kimlik) + domain User (profil) AYNI transaction'da, AYNI Guid Id ile.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.DisplayName))
            return BadRequest("Email, Password ve DisplayName zorunlu.");

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
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !await _users.CheckPasswordAsync(user, req.Password))
            return Unauthorized("E-posta veya parola hatalı.");

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
        if (user is null) return BadRequest("Geçersiz doğrulama bağlantısı.");

        if (user.EmailConfirmed)
        {
            // Zaten doğrulanmış (linke ikinci tıklama) — hata verme, doğrudan giriş sağla.
            return await IssueForConfirmedAsync(user);
        }

        string decodedToken;
        try { decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(req.Token)); }
        catch { return BadRequest("Geçersiz doğrulama bağlantısı."); }

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
            var user = await _users.FindByEmailAsync(req.Email);
            if (user is not null && !user.EmailConfirmed)
            {
                var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
                await SendConfirmationEmailAsync(user, domainUser?.DisplayName ?? user.Email!, req.Lang);
            }
        }
        return Ok(); // her durumda 200
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
