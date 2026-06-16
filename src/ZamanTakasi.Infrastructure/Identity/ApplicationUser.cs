using Microsoft.AspNetCore.Identity;

namespace ZamanTakasi.Infrastructure.Identity;

/// <summary>
/// Kimlik/parola katmanı (ASP.NET Core Identity). Domain <c>User</c> ile AYNI Guid Id'yi paylaşır (1:1).
/// Profil verisi (DisplayName/ReputationScore) domain User'da tutulur; burada yalnızca kimlik bilgisi vardır.
/// Böylece domain, Identity'ye bağımlı olmadan saf kalır; auth ileride değiştirilebilir.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
}
