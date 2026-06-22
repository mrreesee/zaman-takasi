using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Web.Services;

/// <summary>API'yi tüketen tek nokta. Token AuthState'ten alınıp her isteğe Bearer olarak eklenir.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() } // enum'lar string (API ile uyumlu)
    };

    private readonly HttpClient _http;
    private readonly AuthState _auth;

    public ApiClient(HttpClient http, AuthState auth)
    {
        _http = http;
        _auth = auth;
    }

    // ---- Auth ----
    public Task<AuthResponse?> RegisterAsync(RegisterRequest r) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", r);
    public Task<AuthResponse?> LoginAsync(LoginRequest r) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", r);

    // ---- Listings ----
    public Task<List<ListingDto>?> GetListingsAsync() => SendAsync<List<ListingDto>>(HttpMethod.Get, "api/listings");
    public Task<ListingDto?> CreateListingAsync(CreateListingRequest r) => SendAsync<ListingDto>(HttpMethod.Post, "api/listings", r);
    public Task DeactivateListingAsync(Guid id) => SendAsync<object>(HttpMethod.Post, $"api/listings/{id}/deactivate");

    // ---- Bookings ----
    public Task<BookingDto?> CreateBookingAsync(CreateBookingRequest r) => SendAsync<BookingDto>(HttpMethod.Post, "api/bookings", r);
    public Task<BookingDto?> AcceptAsync(Guid id) => SendAsync<BookingDto>(HttpMethod.Post, $"api/bookings/{id}/accept");
    public Task<BookingDto?> CompleteAsync(Guid id) => SendAsync<BookingDto>(HttpMethod.Post, $"api/bookings/{id}/complete");
    public Task<BookingDto?> CancelAsync(Guid id) => SendAsync<BookingDto>(HttpMethod.Post, $"api/bookings/{id}/cancel");
    public Task<List<BookingDto>?> MyBookingsAsync() => SendAsync<List<BookingDto>>(HttpMethod.Get, "api/bookings/mine");

    // ---- Wallet / Users ----
    public Task<WalletDto?> GetWalletAsync() => SendAsync<WalletDto>(HttpMethod.Get, "api/wallet");
    public Task<UserProfileDto?> MeAsync() => SendAsync<UserProfileDto>(HttpMethod.Get, "api/users/me");

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_auth.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        if (body is not null)
            req.Content = JsonContent.Create(body, options: Json);

        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            throw new ApiException((int)res.StatusCode, await ReadErrorAsync(res));

        if (typeof(T) == typeof(object)) return default;
        return await res.Content.ReadFromJsonAsync<T>(Json);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage res)
    {
        try
        {
            var problem = await res.Content.ReadFromJsonAsync<ProblemLike>(Json);
            if (!string.IsNullOrWhiteSpace(problem?.Detail)) return problem!.Detail!;
            if (!string.IsNullOrWhiteSpace(problem?.Title)) return problem!.Title!;
        }
        catch { /* JSON değilse aşağıya düş */ }
        return $"İstek başarısız ({(int)res.StatusCode}).";
    }

    private sealed record ProblemLike(string? Title, string? Detail);
}
