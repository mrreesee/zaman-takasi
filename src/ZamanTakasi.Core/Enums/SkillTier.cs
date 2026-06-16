namespace ZamanTakasi.Core.Enums;

/// <summary>
/// Uzmanlık seviyesi ve aynı zamanda kredi çarpanı.
/// 1 saat hizmet = 1 * (int)Tier zaman kredisi (ZK). MVP'de çarpan SABİTtir.
/// </summary>
public enum SkillTier
{
    Basic = 1,
    Verified = 2,
    Professional = 3
}
