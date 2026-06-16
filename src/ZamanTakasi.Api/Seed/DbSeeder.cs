using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Core;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Core.Enums;
using ZamanTakasi.Infrastructure.Identity;
using ZamanTakasi.Infrastructure.Persistence;

namespace ZamanTakasi.Api.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        // 1) Platform sistem kullanıcısı (domain User; bununla giriş yapılamaz).
        if (!await db.DomainUsers.AnyAsync(u => u.Id == SystemAccounts.PlatformUserId))
        {
            db.DomainUsers.Add(new User(SystemAccounts.PlatformUserId, SystemAccounts.PlatformDisplayName));
            await db.SaveChangesAsync();
        }

        // 2) Demo veriler yalnızca platform dışında kullanıcı yoksa eklenir.
        if (await db.DomainUsers.CountAsync() > 1) return;

        var alice = await CreateUserAsync(users, db, "alice@demo.local", "Passw0rd!", "Alice");
        var bob = await CreateUserAsync(users, db, "bob@demo.local", "Passw0rd!", "Bob");

        // Açılış bakiyesi (kapalı devre test kredisi).
        db.LedgerEntries.Add(new LedgerEntry(alice.Id, 10m, LedgerEntryType.OpeningBalance));
        db.LedgerEntries.Add(new LedgerEntry(bob.Id, 10m, LedgerEntryType.OpeningBalance));

        // Örnek ilanlar.
        db.Listings.Add(new ServiceListing(bob.Id, "Web sitesi kurulumu", "Statik site + GitHub Pages yayını.", SkillTier.Verified));
        db.Listings.Add(new ServiceListing(alice.Id, "İngilizce konuşma pratiği", "30–60 dk seans.", SkillTier.Basic));

        await db.SaveChangesAsync();
    }

    private static async Task<User> CreateUserAsync(UserManager<ApplicationUser> users, AppDbContext db, string email, string password, string name)
    {
        var appUser = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email };
        var result = await users.CreateAsync(appUser, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Seed kullanıcı oluşturulamadı: " + string.Join("; ", result.Errors.Select(e => e.Description)));

        var user = new User(appUser.Id, name);
        db.DomainUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
