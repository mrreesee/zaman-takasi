using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Npgsql;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Web.Components;
using ZamanTakasi.Web.Infrastructure;
using ZamanTakasi.Web.Localization;
using ZamanTakasi.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Bulut (Railway/Heroku vb.): PORT verilmişse o porta bağlan — tek public origin.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Data Protection anahtarlari (ProtectedLocalStorage'daki oturum + antiforgery) Postgres'e kalici yazilir;
// yoksa her deploy'da herkes cikis yapmis olur. DATABASE_URL (Railway referans degiskeni) ya da
// ConnectionStrings:Default verilmemisse gecici (container-yerel) anahtarlarla devam eder ve uyari loglanir.
var dpConn = NpgsqlConnectionResolver.Resolve(
    Environment.GetEnvironmentVariable("DATABASE_URL") ?? builder.Configuration.GetConnectionString("Default"));
builder.Services.AddDataProtection().SetApplicationName("ZamanTakasi.Web");
if (dpConn is not null)
{
    var dataSource = NpgsqlDataSource.Create(dpConn);
    PostgresXmlRepository.EnsureTable(dataSource);
    builder.Services.Configure<KeyManagementOptions>(o => o.XmlRepository = new PostgresXmlRepository(dataSource));
}

// API tabanı (appsettings: "ApiBaseUrl"). UI yalnızca bu API'yi tüketir.
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5053";
builder.Services.AddHttpClient<ApiClient>(c => c.BaseAddress = new Uri(apiBase));
builder.Services.AddScoped<AuthState>();

// i18n: çeviri servisi.
builder.Services.AddSingleton<Loc>();

var app = builder.Build();

if (dpConn is null)
    app.Logger.LogWarning("DATABASE_URL yok: Data Protection anahtarlari KALICI DEGIL; her deploy'da kullanici oturumlari duser.");

// Railway/ters proxy arkasinda gercek sema (https) ve istemci IP'si basliklardan alinir (HSTS/Secure cookie dogru calissin).
var fwd = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
fwd.KnownIPNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

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
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                Path = "/",
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax
            });
    }
    return Results.LocalRedirect(string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
