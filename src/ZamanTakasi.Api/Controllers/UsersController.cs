using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Api.Auth;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Infrastructure.Services;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBalanceService _balances;

    public UsersController(AppDbContext db, IBalanceService balances)
    {
        _db = db;
        _balances = balances;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me()
    {
        var me = User.GetUserId();
        var user = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == me)
                   ?? throw new NotFoundException("Kullanıcı bulunamadı.");
        var balance = await _balances.GetBalanceAsync(me);
        return Ok(new UserProfileDto(user.Id, user.DisplayName, user.ReputationScore, user.CreatedAt, balance));
    }

    /// <summary>
    /// TEST AMAÇLI: açılış bakiyesi yükler (kapalı devre kredi; NAKİT DEĞİL, TL'ye çevrilemez).
    /// Gerçek ürün akışında kredi yalnızca hizmet kazancından gelir.
    /// </summary>
    [HttpPost("me/opening-balance")]
    public async Task<ActionResult<UserProfileDto>> AddOpeningBalance(OpeningBalanceRequest req)
    {
        if (req.Amount <= 0) throw new DomainException("Açılış bakiyesi pozitif olmalı.");
        var me = User.GetUserId();
        _db.LedgerEntries.Add(new LedgerEntry(me, req.Amount, LedgerEntryType.OpeningBalance));
        await _db.SaveChangesAsync();

        var user = await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == me)
                   ?? throw new NotFoundException("Kullanıcı bulunamadı.");
        var balance = await _balances.GetBalanceAsync(me);
        return Ok(new UserProfileDto(user.Id, user.DisplayName, user.ReputationScore, user.CreatedAt, balance));
    }
}
