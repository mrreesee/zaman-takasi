using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Api.Auth;
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

    // NOT: Eski 'POST me/opening-balance' ucu KALDIRILDI. Kullanıcının kendine kredi basması
    // (self top-up) bir backdoor'du. Kapalı betada açılış bakiyesi yalnızca kayıt anında, otomatik
    // ve bir kez verilir (bkz. AuthController.Register -> IWelcomeBalanceService). Krediler kapalı
    // devredir; nakit/TL ile satın alınamaz.
}
