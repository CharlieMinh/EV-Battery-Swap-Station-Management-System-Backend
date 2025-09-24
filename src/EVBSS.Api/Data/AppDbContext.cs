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

    // Payment & Invoice System
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
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
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.MonthlyFeeUnder1500Km).HasPrecision(18, 2);
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.MonthlyFee1500To3000Km).HasPrecision(18, 2);
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.MonthlyFeeOver3000Km).HasPrecision(18, 2);
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.DepositAmount).HasPrecision(18, 2);
        b.Entity<SubscriptionPlan>()
            .Property(sp => sp.OverdueInterestRate).HasPrecision(5, 4);
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
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserSubscription>()
            .HasOne(us => us.SubscriptionPlan)
            .WithMany()
            .HasForeignKey(us => us.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserSubscription>()
            .HasOne(us => us.Vehicle)
            .WithMany()
            .HasForeignKey(us => us.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Invoice
        b.Entity<Invoice>()
            .HasIndex(i => i.InvoiceNumber).IsUnique();
        b.Entity<Invoice>()
            .Property(i => i.InvoiceNumber).HasMaxLength(50);
        b.Entity<Invoice>()
            .Property(i => i.SubtotalAmount).HasPrecision(18, 2);
        b.Entity<Invoice>()
            .Property(i => i.TaxAmount).HasPrecision(18, 2);
        b.Entity<Invoice>()
            .Property(i => i.TotalAmount).HasPrecision(18, 2);
        b.Entity<Invoice>()
            .Property(i => i.PaidAmount).HasPrecision(18, 2);
        b.Entity<Invoice>()
            .Property(i => i.OverdueFeeAmount).HasPrecision(18, 2);
        b.Entity<Invoice>()
            .HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Invoice>()
            .HasOne(i => i.UserSubscription)
            .WithMany(us => us.Invoices)
            .HasForeignKey(i => i.UserSubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Payment
        b.Entity<Payment>()
            .HasIndex(p => p.PaymentReference).IsUnique();
        b.Entity<Payment>()
            .Property(p => p.PaymentReference).HasMaxLength(100);
        b.Entity<Payment>()
            .Property(p => p.Amount).HasPrecision(18, 2);
        b.Entity<Payment>()
            .HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
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
            .HasOne(st => st.Invoice)
            .WithMany(i => i.SwapTransactions)
            .HasForeignKey(st => st.InvoiceId)
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
    }
}
