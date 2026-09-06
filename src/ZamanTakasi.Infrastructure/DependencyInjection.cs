using System.Net.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZamanTakasi.Core.Abstractions;
using ZamanTakasi.Core.Services;
using ZamanTakasi.Infrastructure.Identity;
using ZamanTakasi.Infrastructure.Persistence;
using ZamanTakasi.Infrastructure.Services;
using ZamanTakasi.Infrastructure.Stubs;

namespace ZamanTakasi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

        // Data Protection anahtarlarını Postgres'e kalıcı yaz: Identity e-posta doğrulama token'ları
        // redeploy'lar arasında geçerli kalsın (aksi halde her deploy'da bekleyen linkler geçersizleşir).
        services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();

        // JWT kullandığımız için cookie tabanlı AddIdentity yerine AddIdentityCore yeterli.
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 8;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequireUppercase = false;
            // Kaba kuvvet koruması: 5 hatalı denemede 15 dk kilit. NOT: UserManager.CheckPasswordAsync kilidi
            // KENDİLİĞİNDEN uygulamaz; AuthController.Login IsLockedOut/AccessFailed çağrılarıyla uygular.
            o.Lockout.AllowedForNewUsers = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            // E-posta doğrulama token üreticisi (GenerateEmailConfirmationTokenAsync) için gerekli.
            o.SignIn.RequireConfirmedEmail = false; // kapı uygulama katmanında AuthOptions ile yönetilir
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<LedgerOptions>(config.GetSection("Ledger"));

        // E-posta (Resend) ve kayıt kapısı yapılandırması. Hassas değerler (ApiKey) env'den gelir.
        services.Configure<ResendOptions>(config.GetSection("Resend"));
        services.Configure<AuthOptions>(config.GetSection("Auth"));

        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWelcomeBalanceService, WelcomeBalanceService>();

        // Gerçek bildirim: e-posta doğrulama Resend ile gönderilir (ApiKey yoksa loglar). Booking bildirimleri loglanır.
        // Header'lar HttpRequestMessage üzerinde ayarlandığı için tek bir HttpClient paylaşımı thread-safe'tir.
        services.AddSingleton(new HttpClient());
        services.AddScoped<INotificationService, ResendNotificationService>();

        // KAPSAM DIŞI port'lar — stub kayıtları (gerçek implementasyon sonraki aşama).
        services.AddScoped<IPaymentService, PaymentServiceStub>();
        services.AddScoped<IKycService, KycServiceStub>();

        return services;
    }
}
