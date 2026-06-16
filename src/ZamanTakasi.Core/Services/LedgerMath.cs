namespace ZamanTakasi.Core.Services;

public static class LedgerMath
{
    /// <summary>
    /// Tüm parasal yuvarlama tek noktadan: 2 ondalık, banker's rounding (MidpointRounding.ToEven).
    /// </summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);
}
