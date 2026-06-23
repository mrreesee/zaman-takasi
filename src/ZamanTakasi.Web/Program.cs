using System.Globalization;
using Microsoft.AspNetCore.Localization;
using ZamanTakasi.Web.Components;
using ZamanTakasi.Web.Localization;
using ZamanTakasi.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Bulut (Railway/Heroku vb.): PORT verilmişse o porta bağlan — tek public origin.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API tabanı (appsettings: "ApiBaseUrl"). UI yalnızca bu API'yi tüketir.
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5053";
builder.Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(apiBase));
builder.Services.AddScoped<AuthState>();

// i18n: çeviri servisi.
builder.Services.AddSingleton<Loc>();

var app = builder.Build();

// Kültür: varsayılan İngilizce; cookie ya da tarayıcı diline göre (en/tr). Switcher cookie'yi yazar.
var supportedCultures = Translations.SupportedCultures.Select(c => new CultureInfo(c)).ToList();
var locOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(Translations.DefaultCulture),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
// Ana dil İngilizce: tarayıcı dilini OTOMATİK algılama; yalnızca kullanıcı seçimi (cookie) + URL geçerli.
locOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new QueryStringRequestCultureProvider(),
    new CookieRequestCultureProvider()
};
app.UseRequestLocalization(locOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

// Dil değiştir: cookie yazıp geldiği sayfaya döner (tam yeniden yükleme — Blazor Server kültürü tazeler).
app.MapGet("/Culture/Set", (string culture, string? redirectUri, HttpContext http) =>
{
    if (Translations.SupportedCultures.Contains(culture))
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, Path = "/" });
    }
    return Results.LocalRedirect(string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
