using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Api.Auth;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Exceptions;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ListingsController(AppDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> GetAll([FromQuery] bool activeOnly = true)
    {
        var q = _db.Listings.AsQueryable();
        if (activeOnly) q = q.Where(l => l.IsActive);

        var items = await (from l in q
                           join u in _db.DomainUsers on l.ProviderUserId equals u.Id into gj
                           from u in gj.DefaultIfEmpty()
                           orderby l.CreatedAt descending
                           select new ListingDto(
                               l.Id, l.ProviderUserId, u != null ? u.DisplayName : "—",
                               l.Title, l.Description, l.Tier, (int)l.Tier, l.IsActive, l.CreatedAt))
                          .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ListingDto>> Get(Guid id)
    {
        var l = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("İlan bulunamadı.");
        var name = (await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == l.ProviderUserId))?.DisplayName ?? "—";
        return Ok(l.ToDto(name));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ListingDto>> Create(CreateListingRequest req)
    {
        var providerId = User.GetUserId();
        var listing = new ServiceListing(providerId, req.Title, req.Description, req.Tier);
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();

        var name = (await _db.DomainUsers.FirstOrDefaultAsync(u => u.Id == providerId))?.DisplayName ?? "—";
        return CreatedAtAction(nameof(Get), new { id = listing.Id }, listing.ToDto(name));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id)
                      ?? throw new NotFoundException("İlan bulunamadı.");
        if (listing.ProviderUserId != User.GetUserId())
            throw new ForbiddenActionException("Yalnızca ilan sahibi pasifleştirebilir.");
        listing.Deactivate();
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
