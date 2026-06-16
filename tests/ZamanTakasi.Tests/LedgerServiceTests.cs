using ZamanTakasi.Core;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Core.Exceptions;
using ZamanTakasi.Core.Services;

namespace ZamanTakasi.Tests;

public class LedgerServiceTests
{
    private readonly ILedgerService _ledger = new LedgerService();

    private static Booking AcceptedBooking(decimal hours, SkillTier tier)
    {
        var b = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), hours, tier);
        b.Accept();
        return b;
    }

    [Fact]
    public void Completion_entries_sum_to_zero()
    {
        var b = AcceptedBooking(2m, SkillTier.Professional); // cost = 6
        var entries = _ledger.BuildCompletionEntries(b, 0.13m, requesterBalance: 100m);

        Assert.Equal(3, entries.Count);
        Assert.Equal(0m, entries.Sum(e => e.Amount)); // İŞ KURALI 2: toplam her zaman 0
    }

    [Fact]
    public void Completion_splits_into_payment_earning_commission()
    {
        var b = AcceptedBooking(1m, SkillTier.Professional); // cost = 3
        var entries = _ledger.BuildCompletionEntries(b, 0.13m, 100m);

        var pay = entries.Single(e => e.EntryType == LedgerEntryType.ServicePayment);
        var earn = entries.Single(e => e.EntryType == LedgerEntryType.ServiceEarning);
        var comm = entries.Single(e => e.EntryType == LedgerEntryType.Commission);

        Assert.Equal(-3m, pay.Amount);
        Assert.Equal(b.RequesterUserId, pay.UserId);
        Assert.Equal(b.ProviderUserId, earn.UserId);
        Assert.Equal(SystemAccounts.PlatformUserId, comm.UserId);

        // commission = round(3 * 0.13, 2) = 0.39 ; earning = 3 - 0.39 = 2.61
        Assert.Equal(0.39m, comm.Amount);
        Assert.Equal(2.61m, earn.Amount);
        Assert.Equal(0m, pay.Amount + earn.Amount + comm.Amount);
    }

    [Fact]
    public void Completion_insufficient_balance_throws()
    {
        var b = AcceptedBooking(2m, SkillTier.Professional); // cost = 6
        Assert.Throws<InsufficientBalanceException>(
            () => _ledger.BuildCompletionEntries(b, 0.13m, requesterBalance: 5.99m));
    }

    [Fact]
    public void Completion_requires_accepted_status()
    {
        var pending = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, SkillTier.Basic);
        Assert.Throws<InvalidBookingTransitionException>(
            () => _ledger.BuildCompletionEntries(pending, 0.13m, 100m));
    }

    [Fact]
    public void Balance_equals_sum_of_ledger_entries()
    {
        var b = AcceptedBooking(2m, SkillTier.Professional); // cost = 6
        var entries = _ledger.BuildCompletionEntries(b, 0.13m, requesterBalance: 10m);

        var requesterDelta = entries.Where(e => e.UserId == b.RequesterUserId).Sum(e => e.Amount);
        Assert.Equal(-6m, requesterDelta);

        // bakiye = açılış (10) + defter deltası => 4
        Assert.Equal(4m, 10m + requesterDelta);
    }
}
