using System.Globalization;

namespace ZamanTakasi.Web.Localization;

/// <summary>
/// Arayüz çevirisi. Geçerli kültürü (RequestLocalization tarafından ayarlanan) kullanır.
/// Kullanım: <c>@inject Loc L</c> sonra <c>@L["nav.home"]</c> veya <c>@L.Fmt("home.trust", 1200)</c>.
/// </summary>
public sealed class Loc
{
    public string this[string key] => Translations.Get(CultureInfo.CurrentUICulture.Name, key);

    public string Fmt(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, this[key], args);

    /// <summary>Geçerli dilin iki harfli kodu ("en" / "tr").</summary>
    public string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

    /// <summary>Geçişte hedef dil (mevcut tr ise en, değilse tr).</summary>
    public string OtherLang => Lang == "tr" ? "en" : "tr";
}
