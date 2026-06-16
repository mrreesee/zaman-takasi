namespace ZamanTakasi.Core.Enums;

public enum LedgerEntryType
{
    /// <summary>Açılış / test bakiyesi.</summary>
    OpeningBalance = 0,

    /// <summary>Requester'ın hizmet için ödediği borç (-).</summary>
    ServicePayment = 1,

    /// <summary>Provider'ın hizmetten kazandığı alacak (+).</summary>
    ServiceEarning = 2,

    /// <summary>Platform komisyonu (+), sistem hesabına yazılır.</summary>
    Commission = 3
}
