namespace ZamanTakasi.Shared;

// Lang: kayıt sırasındaki arayüz dili ("en"/"tr"). Doğrulama e-postası bu dilde yazılır.
// Varsayılan değeri olduğu için mevcut 3-parametreli çağrılar bozulmadan derlenir.
public record RegisterRequest(string Email, string Password, string DisplayName, string Lang = "en");

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, Guid UserId, string DisplayName, DateTime ExpiresAtUtc);

/// <summary>
/// Kayıt sonucu. E-posta onayı kapısı AÇIK ise <see cref="RequiresEmailConfirmation"/> true döner
/// ve <see cref="Auth"/> null olur (kullanıcı henüz giriş yapamaz). Kapı KAPALI ise klasik davranış:
/// RequiresEmailConfirmation=false ve Auth dolu (anında giriş).
/// </summary>
public record RegisterResponse(bool RequiresEmailConfirmation, AuthResponse? Auth);

/// <summary>E-posta doğrulama bağlantısındaki userId + token ile onay isteği.</summary>
public record ConfirmEmailRequest(Guid UserId, string Token);

/// <summary>Doğrulama e-postasını yeniden gönderme isteği. Cevap her zaman 200 (hesap varlığını sızdırmaz).</summary>
public record ResendConfirmationRequest(string Email, string Lang = "en");

/// <summary>"Parolamı unuttum": e-postaya sıfırlama bağlantısı ister. Cevap her zaman 200 (hesap varlığını sızdırmaz).</summary>
public record ForgotPasswordRequest(string Email, string Lang = "en");

/// <summary>Sıfırlama bağlantısındaki userId + token ile yeni parola belirleme.</summary>
public record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
