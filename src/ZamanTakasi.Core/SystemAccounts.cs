namespace ZamanTakasi.Core;

/// <summary>
/// Platforma ait rezerve SİSTEM hesabı. Komisyon defter kayıtları bu kullanıcıya yazılır.
/// Bu hesapla giriş yapılamaz; yalnızca defter bütünlüğü için vardır (tüm Amount toplamı = 0).
/// </summary>
public static class SystemAccounts
{
    public static readonly Guid PlatformUserId = new("00000000-0000-0000-0000-000000000001");
    public const string PlatformDisplayName = "Zaman Takası (Platform)";
}
