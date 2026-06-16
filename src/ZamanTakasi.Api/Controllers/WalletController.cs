using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZamanTakasi.Api.Auth;
using ZamanTakasi.Infrastructure.Services;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Api.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public sealed class WalletController : ControllerBase
{
    private readonly IBalanceService _balances;
    public WalletController(IBalanceService balances) => _balances = balances;

    /// <summary>Güncel bakiye (defter toplamı) + işlem geçmişi.</summary>
    [HttpGet]
    public async Task<ActionResult<WalletDto>> Get()
    {
        var me = User.GetUserId();
        var balance = await _balances.GetBalanceAsync(me);
        var entries = await _balances.GetEntriesAsync(me);
        return Ok(new WalletDto(me, balance, entries.Select(e => e.ToDto()).ToList()));
    }
}
