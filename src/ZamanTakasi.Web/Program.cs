using ZamanTakasi.Web.Components;
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
