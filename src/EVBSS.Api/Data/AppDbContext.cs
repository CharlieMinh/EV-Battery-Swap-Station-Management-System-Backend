using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<User> Users => Set<User>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<BatteryModel> BatteryModels => Set<BatteryModel>();
    public DbSet<BatteryUnit> BatteryUnits => Set<BatteryUnit>();
    public DbSet<BatteryInventory> BatteryInventories => Set<BatteryInventory>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    // Payment & Subscription System (✅ Invoice removed)
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SwapTransaction> SwapTransactions => Set<SwapTransaction>();
    
    // Password Reset System
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Station
        b.Entity<Station>().HasIndex(s => new { s.City, s.IsActive });
        b.Entity<Station>().Property(s => s.Name).HasMaxLength(200);
        b.Entity<Station>().Property(s => s.Address).HasMaxLength(500);
        b.Entity<Station>().Property(s => s.City).HasMaxLength(100);
        b.Entity<Station>().Property(s => s.PhoneNumber).HasMaxLength(20);
        b.Entity<Station>().Property(s => s.PrimaryImageUrl).HasMaxLength(500);

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

        // VehicleModel (Loại xe của hãng)
        b.Entity<VehicleModel>()
            .HasIndex(vm => vm.Name).IsUnique();
        b.Entity<VehicleModel>()
            .Property(vm => vm.Name).HasMaxLength(100);
        b.Entity<VehicleModel>()
            .Property(vm => vm.FullName).HasMaxLength(200);
        b.Entity<VehicleModel>()
            .Property(vm => vm.Brand).HasMaxLength(100);
        b.Entity<VehicleModel>()
            .HasOne(vm => vm.CompatibleBatteryModel)
            .WithMany()
            .HasForeignKey(vm => vm.CompatibleBatteryModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // Vehicle
        b.Entity<Vehicle>().Property(v => v.VIN).HasMaxLength(17);
        b.Entity<Vehicle>().Property(v => v.Plate).HasMaxLength(20);
        b.Entity<Vehicle>().Property(v => v.PhotoUrl).HasMaxLength(500);

        b.Entity<Vehicle>().HasIndex(v => new { v.UserId, v.VIN }).IsUnique();
        b.Entity<Vehicle>().HasIndex(v => new { v.UserId, v.Plate }).IsUnique();

        b.Entity<Vehicle>()
            .HasOne(v => v.User)
            .WithMany(u => u.Vehicles)
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Vehicle>()
            .HasOne(v => v.VehicleModel)
            .WithMany()
            .HasForeignKey(v => v.VehicleModelId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Vehicle>()
            .HasOne(v => v.CompatibleModel)
            .WithMany()
            .HasForeignKey(v => v.CompatibleBatteryModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // BatteryUnit
        b.Entity<BatteryUnit>().HasIndex(u => u.Serial).IsUnique();
        b.Entity<BatteryUnit>().HasIndex(u => new { u.StationId, u.Status, u.IsReserved }); // tìm nhanh "Full & !IsReserved"
        b.Entity<BatteryUnit>().Property(u => u.IsReserved).HasDefaultValue(false);

        // BatteryInventory - HYBRID SOLUTION for quantity-based management
        b.Entity<BatteryInventory>()
            .HasIndex(bi => new { bi.BatteryModelId, bi.StationId, bi.Status })
            .IsUnique(); // Unique constraint: only one record per (Model, Station, Status)
        
        b.Entity<BatteryInventory>()
            .HasOne(bi => bi.BatteryModel)
            .WithMany()
            .HasForeignKey(bi => bi.BatteryModelId)
            .OnDelete(DeleteBehavior.Restrict);
        
        b.Entity<BatteryInventory>()
            .HasOne(bi => bi.Station)
            .WithMany()
            .HasForeignKey(bi => bi.StationId)
            .OnDelete(DeleteBehavior.Restrict);
        
        b.Entity<BatteryInventory>()
            .Property(bi => bi.Quantity)
            .HasDefaultValue(0);
        
        b.Entity<BatteryInventory>()
            .Property(bi => bi.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

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

        // Payment & Invoice System Configurations
        ConfigurePaymentSystem(b);

        base.OnModelCreating(b);
    }

    private void ConfigurePaymentSystem(ModelBuilder b)
    {
        // SubscriptionPlan
        b.Entity<SubscriptionPlan>()
            .HasIndex(sp => sp.Name).IsUnique();
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.Name).HasMaxLength(200);
        // ✅ SIMPLIFIED PRICING - No deposit fields
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.MonthlyPrice).HasPrecision(18, 2);
        b.Entity<SubscriptionPlan>()
            .HasOne(sp => sp.BatteryModel)
            .WithMany()
            .HasForeignKey(sp => sp.BatteryModelId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserSubscription
        b.Entity<UserSubscription>()
            .HasIndex(us => new { us.UserId, us.VehicleId, us.IsActive });
        b.Entity<UserSubscription>()
            .Property(us => us.DepositPaid).HasPrecision(18, 2);
        b.Entity<UserSubscription>()
            .HasOne(us => us.User)
            .WithMany(u => u.UserSubscriptions)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserSubscription>()
            .HasOne(us => us.SubscriptionPlan)
            .WithMany(sp => sp.UserSubscriptions)
            .HasForeignKey(us => us.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserSubscription>()
            .HasOne(us => us.Vehicle)
            .WithMany(v => v.UserSubscriptions)
            .HasForeignKey(us => us.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Payment (✅ Refactored to link with Subscription)
        b.Entity<Payment>()
            .HasIndex(p => p.PaymentReference).IsUnique();
        b.Entity<Payment>()
            .Property(p => p.PaymentReference).HasMaxLength(100);
        b.Entity<Payment>()
            .Property(p => p.Description).HasMaxLength(500);
        b.Entity<Payment>()
            .Property(p => p.Amount).HasPrecision(18, 2);
        b.Entity<Payment>()
            .HasOne(p => p.UserSubscription)
            .WithMany()
            .HasForeignKey(p => p.UserSubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Payment>()
            .HasOne(p => p.ProcessedByStaff)
            .WithMany()
            .HasForeignKey(p => p.ProcessedByStaffId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<Payment>()
            .HasOne(p => p.Station)
            .WithMany()
            .HasForeignKey(p => p.StationId)
            .OnDelete(DeleteBehavior.SetNull);

        // SwapTransaction
        b.Entity<SwapTransaction>()
            .HasIndex(st => st.TransactionNumber).IsUnique();
        b.Entity<SwapTransaction>()
            .Property(st => st.TransactionNumber).HasMaxLength(50);
        b.Entity<SwapTransaction>()
            .Property(st => st.SwapFee).HasPrecision(18, 2);
        b.Entity<SwapTransaction>()
            .Property(st => st.KmChargeAmount).HasPrecision(18, 2);
        b.Entity<SwapTransaction>()
            .Property(st => st.TotalAmount).HasPrecision(18, 2);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.User)
            .WithMany()
            .HasForeignKey(st => st.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.Reservation)
            .WithMany()
            .HasForeignKey(st => st.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.Station)
            .WithMany()
            .HasForeignKey(st => st.StationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.Vehicle)
            .WithMany()
            .HasForeignKey(st => st.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.UserSubscription)
            .WithMany()
            .HasForeignKey(st => st.UserSubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.IssuedBattery)
            .WithMany()
            .HasForeignKey(st => st.IssuedBatteryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SwapTransaction>()
            .HasOne(st => st.ReturnedBattery)
            .WithMany()
            .HasForeignKey(st => st.ReturnedBatteryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure PasswordResetToken
        b.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.OtpHash)
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(e => e.CreatedAt)
                .IsRequired();
                
            entity.Property(e => e.ExpiresAt)
                .IsRequired();
                
            entity.Property(e => e.IsUsed)
                .IsRequired()
                .HasDefaultValue(false);
                
            entity.Property(e => e.AttemptCount)
                .IsRequired()
                .HasDefaultValue(0);
                
            entity.Property(e => e.RequestIpAddress)
                .HasMaxLength(45); // IPv6 max length
                
            entity.Property(e => e.RequestUserAgent)
                .HasMaxLength(500);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsUsed, e.ExpiresAt });
            entity.HasIndex(e => e.ExpiresAt); // For cleanup jobs
        });
    }
}
