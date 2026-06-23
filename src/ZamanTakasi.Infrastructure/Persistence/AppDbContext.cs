using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZamanTakasi.Core.Entities;
using ZamanTakasi.Infrastructure.Identity;

namespace ZamanTakasi.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // NOT: IdentityDbContext zaten 'Users' (ApplicationUser) DbSet'ini içerir; çakışmamak için domain
    // kullanıcıları 'DomainUsers' adıyla, ama "Users" tablosuyla eşlenir.
    public DbSet<User> DomainUsers => Set<User>();
    public DbSet<ServiceListing> Listings => Set<ServiceListing>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); // Identity tabloları (AspNetUsers vb.)

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever(); // Id, ApplicationUser ile paylaşılan Guid
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);
        });

        b.Entity<ServiceListing>(e =>
        {
            e.ToTable("Listings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(160);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Tier).HasConversion<int>();
            // İçerik dili ("en"/"tr"); mevcut satırlar Türkçe içerik olduğu için varsayılan "tr".
            e.Property(x => x.Language).IsRequired().HasMaxLength(8).HasDefaultValue("tr");
            e.HasIndex(x => x.IsActive);
            e.HasIndex(x => x.ProviderUserId);
            e.HasIndex(x => x.Language);
        });

        b.Entity<Booking>(e =>
        {
            e.ToTable("Bookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            // Postgres numeric(18,2); yuvarlama uygulama katmanında banker's (ToEven).
            e.Property(x => x.Hours).HasPrecision(18, 2);
            e.Property(x => x.CreditCost).HasPrecision(18, 2);
            e.HasIndex(x => x.RequesterUserId);
            e.HasIndex(x => x.ProviderUserId);
            e.HasIndex(x => x.Status);
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable("LedgerEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntryType).HasConversion<int>();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => x.UserId);          // bakiye = UserId'ye göre toplam
            e.HasIndex(x => x.BookingId);
            // Kayıtlar DEĞİŞMEZ: uygulama katmanı asla update/delete yapmaz.
        });
    }
}
