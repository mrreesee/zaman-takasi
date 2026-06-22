using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Api.Auth;
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

    public AuthController(UserManager<ApplicationUser> users, AppDbContext db, JwtTokenService jwt, IWelcomeBalanceService welcome)
    {
        _users = users;
        _db = db;
        _jwt = jwt;
        _welcome = welcome;
    }

    /// <summary>Yeni kullanıcı: ApplicationUser (kimlik) + domain User (profil) AYNI transaction'da, AYNI Guid Id ile.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
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

        // Cold-start: kayıt anında bir kerelik Hoş Geldin bakiyesi (OpeningBalance LedgerEntry).
        // Aynı transaction içinde -> kullanıcı ve bakiyesi atomik oluşur.
        await _welcome.GrantIfFirstTimeAsync(appUser.Id);

        await tx.CommitAsync();

        var (token, exp) = _jwt.Create(appUser.Id, req.Email, domainUser.DisplayName);
        return Ok(new AuthResponse(token, appUser.Id, domainUser.DisplayName, exp));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !await _users.CheckPasswordAsync(user, req.Password))
            return Unauthorized("E-posta veya parola hatalı.");

        var domainUser = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
        var name = domainUser?.DisplayName ?? user.Email!;
        var (token, exp) = _jwt.Create(user.Id, user.Email!, name);
        return Ok(new AuthResponse(token, user.Id, name, exp));
    }
}
