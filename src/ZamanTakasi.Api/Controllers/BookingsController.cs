using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Api.Auth;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Infrastructure.Services;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;
    private readonly AppDbContext _db;

    public BookingsController(IBookingService bookings, AppDbContext db)
    {
        _bookings = bookings;
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest req)
    {
        var booking = await _bookings.CreateAsync(User.GetUserId(), req.ListingId, req.Hours);
        return Ok(booking.ToDto());
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<BookingDto>> Accept(Guid id)
        => Ok((await _bookings.AcceptAsync(id, User.GetUserId())).ToDto());

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<BookingDto>> Complete(Guid id)
        => Ok((await _bookings.CompleteAsync(id, User.GetUserId())).ToDto());

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(Guid id)
        => Ok((await _bookings.CancelAsync(id, User.GetUserId())).ToDto());

    /// <summary>Kullanıcının taraf olduğu (gelen + giden) rezervasyonlar.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> Mine()
    {
        var me = User.GetUserId();
        var items = await _db.Bookings
            .Where(b => b.RequesterUserId == me || b.ProviderUserId == me)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
        return Ok(items.Select(b => b.ToDto()).ToList());
    }
}
