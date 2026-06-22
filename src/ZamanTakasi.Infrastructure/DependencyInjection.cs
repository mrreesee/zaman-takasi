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

        // JWT kullandığımız için cookie tabanlı AddIdentity yerine AddIdentityCore yeterli.
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 6;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequireUppercase = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<LedgerOptions>(config.GetSection("Ledger"));

        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWelcomeBalanceService, WelcomeBalanceService>();

        // Bildirim (stub): gerçek gönderim yok, structured log yazar (bkz. NotificationServiceStub).
        services.AddScoped<INotificationService, NotificationServiceStub>();

        // KAPSAM DIŞI port'lar — stub kayıtları (gerçek implementasyon sonraki aşama).
        services.AddScoped<IPaymentService, PaymentServiceStub>();
        services.AddScoped<IKycService, KycServiceStub>();

        return services;
    }
}
