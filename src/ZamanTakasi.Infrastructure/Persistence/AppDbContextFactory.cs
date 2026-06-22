using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ZamanTakasi.Infrastructure.Persistence;

/// <summary>
/// Tasarım zamanı (dotnet ef migrations) için DbContext üretir. Migration ÜRETİMİ veritabanına
/// bağlanmaz; bu yüzden buradaki bağlantı yalnızca komut aracına sağlayıcıyı (Npgsql) bildirir.
/// Gerçek bağlantı çalışma zamanında env değişkeninden (DATABASE_URL / ConnectionStrings__Default) gelir.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? "Host=localhost;Port=5432;Database=zamantakasi;Username=postgres;Password=postgres";

        var conn = NpgsqlConnectionResolver.Resolve(raw)!;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new AppDbContext(options);
    }
}
