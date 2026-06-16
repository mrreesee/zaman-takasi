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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default") ?? "Data Source=zamantakasi.db";
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(conn));

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

        // KAPSAM DIŞI port'lar — stub kayıtları (gerçek implementasyon sonraki aşama).
        services.AddScoped<IPaymentService, PaymentServiceStub>();
        services.AddScoped<IKycService, KycServiceStub>();

        return services;
    }
}
