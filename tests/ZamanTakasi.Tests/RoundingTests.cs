using ZamanTakasi.Core.Services;

namespace ZamanTakasi.Tests;

public class RoundingTests
{
    // 2 ondalık + banker's rounding (MidpointRounding.ToEven): orta nokta en yakın ÇİFT'e gider.
    [Theory]
    [InlineData(0.125, 0.12)] // önceki hane 2 (çift) -> aşağı
    [InlineData(0.135, 0.14)] // önceki hane 3 (tek)  -> yukarı
    [InlineData(2.345, 2.34)] // 4 (çift) -> aşağı
    [InlineData(2.355, 2.36)] // 5 (tek)  -> yukarı
    public void Round2_uses_bankers_rounding(decimal input, decimal expected)
        => Assert.Equal(expected, LedgerMath.Round2(input));
}
