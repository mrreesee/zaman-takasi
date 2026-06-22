using ZamanTakasi.Core.Enums;

namespace ZamanTakasi.Shared;

public record LedgerEntryDto(Guid Id, decimal Amount, LedgerEntryType EntryType, Guid? BookingId, DateTime CreatedAt);

public record WalletDto(Guid UserId, decimal Balance, IReadOnlyList<LedgerEntryDto> Entries);
