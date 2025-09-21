using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<BatteryModel> BatteryModels => Set<BatteryModel>();
    public DbSet<BatteryUnit> BatteryUnits => Set<BatteryUnit>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<SwapTransaction> SwapTransactions => Set<SwapTransaction>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Station>().HasIndex(s => new { s.City, s.IsActive });

        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().Property(u => u.Email).HasMaxLength(255);


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

        // Vehicle
        b.Entity<Vehicle>().Property(v => v.VIN).HasMaxLength(17);
        b.Entity<Vehicle>().Property(v => v.Plate).HasMaxLength(20);

        b.Entity<Vehicle>().HasIndex(v => new { v.UserId, v.VIN }).IsUnique();
        b.Entity<Vehicle>().HasIndex(v => new { v.UserId, v.Plate }).IsUnique();

        b.Entity<Vehicle>()
            .HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId);

        b.Entity<Vehicle>()
            .HasOne(v => v.CompatibleModel).WithMany().HasForeignKey(v => v.CompatibleBatteryModelId);

        // BatteryUnit
        b.Entity<BatteryUnit>().HasIndex(u => u.Serial).IsUnique();
        b.Entity<BatteryUnit>().HasIndex(u => new { u.StationId, u.Status, u.IsReserved }); // tìm nhanh "Full & !IsReserved"
        b.Entity<BatteryUnit>().Property(u => u.IsReserved).HasDefaultValue(false);

        // Reservation
        b.Entity<Reservation>().HasIndex(r => new { r.UserId, r.CreatedAt });
        b.Entity<Reservation>().HasIndex(r => new { r.StationId, r.Status });

        b.Entity<Reservation>().HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId);
        b.Entity<Reservation>().HasOne(r => r.Station).WithMany().HasForeignKey(r => r.StationId);
        b.Entity<Reservation>().HasOne(r => r.BatteryModel).WithMany().HasForeignKey(r => r.BatteryModelId);
        b.Entity<Reservation>().HasOne(r => r.BatteryUnit).WithMany().HasForeignKey(r => r.BatteryUnitId);

        b.Entity<Reservation>()
        .HasOne(r => r.User)
        .WithMany()
        .HasForeignKey(r => r.UserId)
        .OnDelete(DeleteBehavior.Restrict);

    b.Entity<Reservation>()
        .HasOne(r => r.Station)
        .WithMany()
        .HasForeignKey(r => r.StationId)
        .OnDelete(DeleteBehavior.Restrict);

    b.Entity<Reservation>()
        .HasOne(r => r.BatteryModel)
        .WithMany()
        .HasForeignKey(r => r.BatteryModelId)
        .OnDelete(DeleteBehavior.Restrict);

    b.Entity<Reservation>()
        .HasOne(r => r.BatteryUnit)
        .WithMany()
        .HasForeignKey(r => r.BatteryUnitId)
        .OnDelete(DeleteBehavior.Restrict);

        // SubscriptionPlan
        b.Entity<SubscriptionPlan>().Property(sp => sp.Name).HasMaxLength(200);
        b.Entity<SubscriptionPlan>().Property(sp => sp.Description).HasMaxLength(500);
        b.Entity<SubscriptionPlan>().Property(sp => sp.Price).HasPrecision(18, 2);

        // UserSubscription
        b.Entity<UserSubscription>().HasIndex(us => new { us.UserId, us.Status });
        b.Entity<UserSubscription>().HasIndex(us => new { us.EndDate, us.Status });
        
        b.Entity<UserSubscription>()
            .HasOne(us => us.User).WithMany().HasForeignKey(us => us.UserId);
        b.Entity<UserSubscription>()
            .HasOne(us => us.SubscriptionPlan).WithMany().HasForeignKey(us => us.SubscriptionPlanId);

        // SwapTransaction
        b.Entity<SwapTransaction>().HasIndex(st => new { st.UserId, st.SwapTime });
        b.Entity<SwapTransaction>().HasIndex(st => new { st.StationId, st.SwapTime });
        b.Entity<SwapTransaction>().HasIndex(st => st.SwapTime);
        b.Entity<SwapTransaction>().Property(st => st.Cost).HasPrecision(18, 2);
        
        b.Entity<SwapTransaction>()
            .HasOne(st => st.User).WithMany().HasForeignKey(st => st.UserId);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.Station).WithMany().HasForeignKey(st => st.StationId);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.OldBattery).WithMany().HasForeignKey(st => st.OldBatteryId);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.NewBattery).WithMany().HasForeignKey(st => st.NewBatteryId);

        base.OnModelCreating(b);
    }
}
