using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZamanTakasi.Infrastructure.Persistence;

/// <summary>
/// Tasarım zamanı (dotnet ef migrations) için DbContext üretir. Çalışma zamanı bağlantısı
/// Api'deki appsettings'ten gelir; bu yalnızca migration komutları içindir.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=zamantakasi.db")
            .Options;
        return new AppDbContext(options);
    }
}
