using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<User>    Users    => Set<User>();

    // 🔽 mới
    public DbSet<BatteryModel> BatteryModels => Set<BatteryModel>();
    public DbSet<BatteryUnit>  BatteryUnits  => Set<BatteryUnit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Station>().HasIndex(s => new { s.City, s.IsActive });

        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().Property(u => u.Email).HasMaxLength(255);

        // 🔽 cấu hình pin
        b.Entity<BatteryModel>()
            .Property(m => m.Name).HasMaxLength(200);
        b.Entity<BatteryUnit>()
            .HasIndex(u => u.Serial).IsUnique();
        b.Entity<BatteryUnit>()
            .HasOne(u => u.Model).WithMany().HasForeignKey(u => u.BatteryModelId);
        b.Entity<BatteryUnit>()
            .HasOne(u => u.Station).WithMany().HasForeignKey(u => u.StationId);
        b.Entity<BatteryUnit>()
            .HasIndex(u => new { u.StationId, u.Status });
    }
}
