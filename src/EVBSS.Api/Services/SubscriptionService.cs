using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Subscriptions;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EVBSS.Api.Services;

public interface ISubscriptionService
{
    Task<SubscriptionCreatedResponse> CreateSubscriptionAsync(Guid userId, CreateSubscriptionRequest request);
    Task<UserSubscriptionDto?> GetUserActiveSubscriptionAsync(Guid userId);
    Task<CancelSubscriptionResponse> CancelSubscriptionAsync(Guid userId);
    Task<SubscriptionUsageDto?> GetSubscriptionUsageAsync(Guid userId);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(AppDbContext context, ILogger<SubscriptionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SubscriptionCreatedResponse> CreateSubscriptionAsync(Guid userId, CreateSubscriptionRequest request)
    {
        // Check if user already has active subscription
        var existingSubscription = await _context.UserSubscriptions
            .Where(us => us.UserId == userId && us.IsActive)
            .FirstOrDefaultAsync();

        if (existingSubscription != null)
        {
            throw new InvalidOperationException("Bạn đã có gói subscription đang hoạt động. Vui lòng hủy gói hiện tại trước khi đăng ký mới.");
        }

        // Validate subscription plan exists and is active
        var subscriptionPlan = await _context.SubscriptionPlans
            .Include(sp => sp.BatteryModel)
            .FirstOrDefaultAsync(sp => sp.Id == request.SubscriptionPlanId && sp.IsActive);
        
        if (subscriptionPlan == null)
        {
            throw new ArgumentException("Gói subscription không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        // Validate vehicle belongs to user and is compatible
        var vehicle = await _context.Vehicles
            .Include(v => v.CompatibleModel)
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.UserId == userId);
        
        if (vehicle == null)
        {
            throw new ArgumentException("Xe không tồn tại hoặc không thuộc về bạn.");
        }

        if (vehicle.CompatibleBatteryModelId != subscriptionPlan.BatteryModelId)
        {
            throw new InvalidOperationException($"Xe {vehicle.Plate} không tương thích với gói pin {subscriptionPlan.Name}.");
        }

        // Calculate billing period (VinFast style: 26th to 25th)
        var startDate = request.StartDate ?? DateTime.UtcNow;
        var (billingStart, billingEnd) = CalculateBillingPeriod(startDate);

        var subscription = new UserSubscription
        {
            UserId = userId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            VehicleId = request.VehicleId,
            StartDate = startDate,
            CurrentBillingPeriodStart = billingStart,
            CurrentBillingPeriodEnd = billingEnd,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created subscription {SubscriptionId} for vehicle {VehicleId}", 
            userId, subscription.Id, request.VehicleId);

        return new SubscriptionCreatedResponse
        {
            SubscriptionId = subscription.Id,
            Message = $"Đăng ký gói {subscriptionPlan.Name} thành công!",
            RequiresDeposit = subscriptionPlan.DepositAmount > 0,
            DepositAmount = subscriptionPlan.DepositAmount,
            StartDate = startDate,
            BillingPeriodStart = billingStart,
            BillingPeriodEnd = billingEnd
        };
    }

    public async Task<UserSubscriptionDto?> GetUserActiveSubscriptionAsync(Guid userId)
    {
        var subscription = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
                .ThenInclude(sp => sp.BatteryModel)
            .Include(us => us.Vehicle)
            .Where(us => us.UserId == userId && us.IsActive)
            .FirstOrDefaultAsync();

        if (subscription == null)
            return null;

        return new UserSubscriptionDto
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            SubscriptionPlanId = subscription.SubscriptionPlanId,
            VehicleId = subscription.VehicleId,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            IsActive = subscription.IsActive,
            CurrentBillingPeriodStart = subscription.CurrentBillingPeriodStart,
            CurrentBillingPeriodEnd = subscription.CurrentBillingPeriodEnd,
            CurrentMonthKmUsed = subscription.CurrentMonthKmUsed,
            DepositPaid = subscription.DepositPaid,
            DepositPaidDate = subscription.DepositPaidDate,
            ConsecutiveOverdueMonths = subscription.ConsecutiveOverdueMonths,
            IsBlocked = subscription.IsBlocked,
            ChargingLimitPercent = subscription.ChargingLimitPercent,
            LastPaymentDate = subscription.LastPaymentDate,
            CreatedAt = subscription.CreatedAt,
            SubscriptionPlan = new SubscriptionPlanDto
            {
                Id = subscription.SubscriptionPlan.Id,
                Name = subscription.SubscriptionPlan.Name,
                Description = subscription.SubscriptionPlan.Description,
                MonthlyFeeUnder1500Km = subscription.SubscriptionPlan.MonthlyFeeUnder1500Km,
                MonthlyFee1500To3000Km = subscription.SubscriptionPlan.MonthlyFee1500To3000Km,
                MonthlyFeeOver3000Km = subscription.SubscriptionPlan.MonthlyFeeOver3000Km,
                DepositAmount = subscription.SubscriptionPlan.DepositAmount,
                BatteryModelId = subscription.SubscriptionPlan.BatteryModelId,
                BatteryModelName = subscription.SubscriptionPlan.BatteryModel.Name,
                IsActive = subscription.SubscriptionPlan.IsActive
            },
            Vehicle = new SubscriptionVehicleDto
            {
                Id = subscription.Vehicle.Id,
                Brand = "VinFast", // Default brand
                Model = "Unknown", // Default model
                VIN = subscription.Vehicle.VIN,
                Plate = subscription.Vehicle.Plate,
                Color = "Unknown", // Default color
                Year = DateTime.UtcNow.Year // Default current year
            }
        };
    }

    public async Task<CancelSubscriptionResponse> CancelSubscriptionAsync(Guid userId)
    {
        var subscription = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.UserId == userId && us.IsActive)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return new CancelSubscriptionResponse
            {
                Success = false,
                Message = "Không tìm thấy gói subscription đang hoạt động."
            };
        }

        // Check for outstanding payments
        var outstandingInvoices = await _context.Invoices
            .Where(i => i.UserSubscriptionId == subscription.Id && 
                       i.Status != Models.PaymentStatus.Completed && 
                       i.Status != Models.PaymentStatus.Cancelled)
            .CountAsync();

        if (outstandingInvoices > 0)
        {
            return new CancelSubscriptionResponse
            {
                Success = false,
                Message = $"Không thể hủy gói. Bạn còn {outstandingInvoices} hóa đơn chưa thanh toán."
            };
        }

        // Cancel subscription
        subscription.IsActive = false;
        subscription.EndDate = DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} cancelled subscription {SubscriptionId}", userId, subscription.Id);

        // Calculate deposit refund (simplified logic)
        decimal? depositRefund = subscription.DepositPaid > 0 ? subscription.DepositPaid : null;

        return new CancelSubscriptionResponse
        {
            Success = true,
            Message = "Hủy gói subscription thành công!",
            EndDate = subscription.EndDate,
            DepositRefund = depositRefund
        };
    }

    public async Task<SubscriptionUsageDto?> GetSubscriptionUsageAsync(Guid userId)
    {
        var subscription = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .Include(us => us.Vehicle)
            .Where(us => us.UserId == userId && us.IsActive)
            .FirstOrDefaultAsync();

        if (subscription == null)
            return null;

        // Get swap transactions for this subscription
        var swapTransactions = await _context.SwapTransactions
            .Where(st => st.UserSubscriptionId == subscription.Id)
            .OrderBy(st => st.StartedAt)
            .ToListAsync();

        // Calculate total statistics (simplified - using VehicleOdoAtSwap as proxy)
        var totalKmUsed = swapTransactions.Count * 100; // Simplified calculation
        var totalAmountPaid = await _context.Invoices
            .Where(i => i.UserSubscriptionId == subscription.Id && i.Status == Models.PaymentStatus.Completed)
            .SumAsync(i => i.TotalAmount);

        // Get current month fee based on usage
        var currentMonthFee = CalculateMonthlyFee(subscription.CurrentMonthKmUsed, subscription.SubscriptionPlan);
        var usageTier = GetUsageTier(subscription.CurrentMonthKmUsed);

        // Calculate monthly breakdown for last 6 months
        var monthlyUsage = await CalculateMonthlyUsageAsync(subscription.Id, swapTransactions);

        return new SubscriptionUsageDto
        {
            SubscriptionId = subscription.Id,
            SubscriptionPlanName = subscription.SubscriptionPlan.Name,
            VehiclePlate = subscription.Vehicle.Plate,
            CurrentBillingPeriodStart = subscription.CurrentBillingPeriodStart,
            CurrentBillingPeriodEnd = subscription.CurrentBillingPeriodEnd,
            CurrentMonthKmUsed = subscription.CurrentMonthKmUsed,
            CurrentMonthFee = currentMonthFee,
            UsageTier = usageTier,
            TotalSwapTransactions = swapTransactions.Count,
            TotalKmUsed = totalKmUsed,
            TotalAmountPaid = totalAmountPaid,
            MonthlyUsage = monthlyUsage
        };
    }

    private static (DateTime start, DateTime end) CalculateBillingPeriod(DateTime referenceDate)
    {
        var today = referenceDate.Date;
        DateTime billingStart, billingEnd;

        if (today.Day >= 26)
        {
            // Current month 26th to next month 25th
            billingStart = new DateTime(today.Year, today.Month, 26);
            billingEnd = billingStart.AddMonths(1).AddDays(-1); // 25th of next month
        }
        else
        {
            // Previous month 26th to current month 25th
            billingEnd = new DateTime(today.Year, today.Month, 25);
            billingStart = billingEnd.AddMonths(-1).AddDays(1); // 26th of previous month
        }

        return (billingStart, billingEnd);
    }

    private static decimal CalculateMonthlyFee(int kmUsed, SubscriptionPlan plan)
    {
        return kmUsed switch
        {
            < 1500 => plan.MonthlyFeeUnder1500Km,
            <= 3000 => plan.MonthlyFee1500To3000Km,
            _ => plan.MonthlyFeeOver3000Km
        };
    }

    private static string GetUsageTier(int kmUsed)
    {
        return kmUsed switch
        {
            < 1500 => "Under1500",
            <= 3000 => "1500To3000",
            _ => "Over3000"
        };
    }

    private async Task<List<MonthlyUsageDto>> CalculateMonthlyUsageAsync(Guid subscriptionId, List<SwapTransaction> swapTransactions)
    {
        var monthlyUsage = new List<MonthlyUsageDto>();
        var today = DateTime.UtcNow;

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = today.AddMonths(-i);
            var (periodStart, periodEnd) = CalculateBillingPeriod(new DateTime(targetMonth.Year, targetMonth.Month, 26));

            var monthTransactions = swapTransactions
                .Where(st => st.StartedAt >= periodStart && st.StartedAt <= periodEnd)
                .ToList();

            var kmUsed = monthTransactions.Count * 100; // Simplified calculation
            
            // Get invoice for this period
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.UserSubscriptionId == subscriptionId &&
                                         i.BillingPeriodStart == periodStart &&
                                         i.BillingPeriodEnd == periodEnd);

            monthlyUsage.Add(new MonthlyUsageDto
            {
                Year = targetMonth.Year,
                Month = targetMonth.Month,
                MonthName = CultureInfo.GetCultureInfo("vi-VN").DateTimeFormat.GetMonthName(targetMonth.Month),
                KmUsed = kmUsed,
                SwapCount = monthTransactions.Count,
                MonthlyFee = invoice?.TotalAmount ?? 0,
                UsageTier = GetUsageTier(kmUsed),
                IsPaid = invoice?.Status == Models.PaymentStatus.Completed
            });
        }

        return monthlyUsage;
    }
}