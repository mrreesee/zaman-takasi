namespace ZamanTakasi.Core.Enums;

/// <summary>
/// Geçerli akış: Pending -> Accepted -> Completed. İptal Pending/Accepted'tan yapılabilir.
/// </summary>
public enum BookingStatus
{
    Pending = 0,
    Accepted = 1,
    Completed = 2,
    Cancelled = 3
}
