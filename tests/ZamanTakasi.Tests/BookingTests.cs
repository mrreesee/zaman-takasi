using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;

namespace ZamanTakasi.Tests;

public class BookingTests
{
    private static Booking New(decimal hours = 1m, SkillTier tier = SkillTier.Basic)
        => Booking.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), hours, tier);

    [Theory]
    [InlineData(2, SkillTier.Professional, 6)]   // 2 * 3
    [InlineData(1.5, SkillTier.Verified, 3)]     // 1.5 * 2
    [InlineData(3, SkillTier.Basic, 3)]          // 3 * 1
    public void CreditCost_is_hours_times_tier(decimal hours, SkillTier tier, decimal expected)
    {
        var b = New(hours, tier);
        Assert.Equal(expected, b.CreditCost);
    }

    [Fact]
    public void New_booking_is_pending()
        => Assert.Equal(BookingStatus.Pending, New().Status);

    [Fact]
    public void Valid_flow_pending_accepted_completed()
    {
        var b = New();
        b.Accept();
        Assert.Equal(BookingStatus.Accepted, b.Status);
        b.Complete();
        Assert.Equal(BookingStatus.Completed, b.Status);
        Assert.NotNull(b.CompletedAt);
    }

    [Fact]
    public void Complete_on_pending_throws()
        => Assert.Throws<InvalidBookingTransitionException>(() => New().Complete());

    [Fact]
    public void Accept_twice_throws()
    {
        var b = New();
        b.Accept();
        Assert.Throws<InvalidBookingTransitionException>(() => b.Accept());
    }

    [Fact]
    public void Cancel_after_completed_throws()
    {
        var b = New();
        b.Accept();
        b.Complete();
        Assert.Throws<InvalidBookingTransitionException>(() => b.Cancel());
    }

    [Fact]
    public void Cannot_book_own_listing()
    {
        var me = Guid.NewGuid();
        Assert.Throws<DomainException>(
            () => Booking.Create(Guid.NewGuid(), me, me, 1m, SkillTier.Basic));
    }
}
