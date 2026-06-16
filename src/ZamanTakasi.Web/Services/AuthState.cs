using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using ZamanTakasi.Shared;

namespace ZamanTakasi.Web.Services;

/// <summary>
/// JWT'yi tarayıcıda ŞİFRELİ (ProtectedLocalStorage / Data Protection) tutar.
/// Prerender kapalı olduğu için JS interop OnInitializedAsync içinde güvenle çağrılabilir.
/// </summary>
public sealed class AuthState
{
    private const string Key = "zt-auth";
    private readonly ProtectedLocalStorage _storage;
    private bool _loaded;

    public string? Token { get; private set; }
    public Guid UserId { get; private set; }
    public string? DisplayName { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

    public event Action? Changed;

    public AuthState(ProtectedLocalStorage storage) => _storage = storage;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        try
        {
            var result = await _storage.GetAsync<StoredAuth>(Key);
            _loaded = true; // JS interop başarılı -> yüklendi say
            if (result.Success && result.Value is not null)
            {
                Token = result.Value.Token;
                UserId = result.Value.UserId;
                DisplayName = result.Value.DisplayName;
                Changed?.Invoke();
            }
        }
        catch
        {
            // Prerender sırasında JS interop yok; _loaded=false kalır, devre başlayınca tekrar denenir.
        }
    }

    public async Task SignInAsync(AuthResponse resp)
    {
        Token = resp.Token;
        UserId = resp.UserId;
        DisplayName = resp.DisplayName;
        await _storage.SetAsync(Key, new StoredAuth(resp.Token, resp.UserId, resp.DisplayName));
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Token = null;
        UserId = default;
        DisplayName = null;
        await _storage.DeleteAsync(Key);
        Changed?.Invoke();
    }

    public sealed record StoredAuth(string Token, Guid UserId, string DisplayName);
}
