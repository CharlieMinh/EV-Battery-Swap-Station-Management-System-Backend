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
    Task CheckAndExpireSubscriptionsAsync(); // ⭐ NEW: Auto-expire logic
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
    
    // ⭐ NEW: Check and expire subscriptions that passed their billing end date
    // Called automatically by middleware on each request, no background job needed
    public async Task CheckAndExpireSubscriptionsAsync()
    {
        var now = DateTime.UtcNow;
        
        // Find active subscriptions that have passed their billing end date
        var expiredSubscriptions = await _context.UserSubscriptions
            .Where(us => us.IsActive && us.CurrentBillingPeriodEnd < now)
            .ToListAsync();
        
        if (!expiredSubscriptions.Any())
            return;
        
        foreach (var subscription in expiredSubscriptions)
        {
            subscription.IsActive = false;
            subscription.UpdatedAt = now;
            
            _logger.LogInformation(
                "Auto-expired subscription {SubscriptionId} for user {UserId}. " +
                "Billing period ended on {EndDate}",
                subscription.Id, subscription.UserId, subscription.CurrentBillingPeriodEnd);
        }
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation(
            "Auto-expired {Count} subscriptions that passed their billing end date", 
            expiredSubscriptions.Count);
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

        // ✅ SIMPLIFIED: 30-day billing period (from start date)
        var startDate = request.StartDate ?? DateTime.UtcNow;
        var billingStart = startDate;
        var billingEnd = startDate.AddDays(30);  // 30 days from now

        var subscription = new UserSubscription
        {
            UserId = userId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            VehicleId = request.VehicleId,
            StartDate = startDate,
            CurrentBillingPeriodStart = billingStart,
            CurrentBillingPeriodEnd = billingEnd,
            CurrentMonthSwapCount = 0,  // ✅ Initialize swap counter
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created subscription {SubscriptionId} for vehicle {VehicleId}, billing {Start} to {End}", 
            userId, subscription.Id, request.VehicleId, billingStart.ToString("yyyy-MM-dd"), billingEnd.ToString("yyyy-MM-dd"));

        return new SubscriptionCreatedResponse
        {
            SubscriptionId = subscription.Id,
            Message = $"Đăng ký gói {subscriptionPlan.Name} thành công!",
            RequiresDeposit = subscriptionPlan.RequiresDeposit,  // ✅ Use new field
            DepositAmount = subscriptionPlan.DepositAmount,
            MonthlyPrice = subscriptionPlan.MonthlyPrice,  // ✅ Add monthly price
            MaxSwapsPerMonth = subscriptionPlan.MaxSwapsPerMonth,  // ✅ Add limit
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
            
            // ✅ SIMPLIFIED: Swap counter instead of km
            CurrentMonthSwapCount = subscription.CurrentMonthSwapCount,
            
            DepositPaid = subscription.DepositPaid,
            DepositPaidDate = subscription.DepositPaidDate,
            LastPaymentDate = subscription.LastPaymentDate,
            CreatedAt = subscription.CreatedAt,
            
            SubscriptionPlan = new SubscriptionPlanDto
            {
                Id = subscription.SubscriptionPlan.Id,
                Name = subscription.SubscriptionPlan.Name,
                Description = subscription.SubscriptionPlan.Description,
                
                // ✅ SIMPLIFIED PRICING
                MonthlyPrice = subscription.SubscriptionPlan.MonthlyPrice,
                MaxSwapsPerMonth = subscription.SubscriptionPlan.MaxSwapsPerMonth,
                RequiresDeposit = subscription.SubscriptionPlan.RequiresDeposit,
                DepositAmount = subscription.SubscriptionPlan.DepositAmount,
                Benefits = subscription.SubscriptionPlan.Benefits,
                RefundPolicy = subscription.SubscriptionPlan.RefundPolicy,
                
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

        // ✅ Check for outstanding payments (refactored to use Payment table)
        var outstandingPayments = await _context.Payments
            .Where(p => p.UserSubscriptionId == subscription.Id && 
                       p.Status == Models.PaymentStatus.Pending)
            .CountAsync();

        if (outstandingPayments > 0)
        {
            return new CancelSubscriptionResponse
            {
                Success = false,
                Message = $"Không thể hủy gói. Bạn còn {outstandingPayments} thanh toán đang chờ xử lý."
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

        // ✅ Calculate total amount paid from Payments table
        var totalAmountPaid = await _context.Payments
            .Where(p => p.UserSubscriptionId == subscription.Id && p.Status == Models.PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        // ✅ FIXED PRICE - No tier calculation needed
        var currentMonthFee = subscription.SubscriptionPlan.MonthlyPrice;
        var plan = subscription.SubscriptionPlan;
        var usageTier = plan.MaxSwapsPerMonth.HasValue 
            ? $"{subscription.CurrentMonthSwapCount}/{plan.MaxSwapsPerMonth} lần"
            : $"{subscription.CurrentMonthSwapCount} lần (không giới hạn)";

        // Calculate monthly breakdown for last 6 months
        var monthlyUsage = await CalculateMonthlyUsageAsync(subscription.Id, swapTransactions);

        return new SubscriptionUsageDto
        {
            SubscriptionId = subscription.Id,
            SubscriptionPlanName = subscription.SubscriptionPlan.Name,
            VehiclePlate = subscription.Vehicle.Plate,
            CurrentBillingPeriodStart = subscription.CurrentBillingPeriodStart,
            CurrentBillingPeriodEnd = subscription.CurrentBillingPeriodEnd,
            
            // ✅ SIMPLIFIED: Swap count instead of km
            CurrentMonthSwapCount = subscription.CurrentMonthSwapCount,
            MaxSwapsPerMonth = plan.MaxSwapsPerMonth,
            
            CurrentMonthFee = currentMonthFee,
            UsageTier = usageTier,
            TotalSwapTransactions = swapTransactions.Count,
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

    // ✅ REMOVED: CalculateMonthlyFee() - No longer needed with fixed pricing
    // ✅ REMOVED: GetUsageTier() - Usage tier now calculated inline based on swap count

    private async Task<List<MonthlyUsageDto>> CalculateMonthlyUsageAsync(Guid subscriptionId, List<SwapTransaction> swapTransactions)
    {
        var monthlyUsage = new List<MonthlyUsageDto>();
        var today = DateTime.UtcNow;

        // Get subscription to access plan details
        var subscription = await _context.UserSubscriptions
            .Include(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);
        
        if (subscription == null) return monthlyUsage;

        for (int i = 5; i >= 0; i--)
        {
            var targetMonth = today.AddMonths(-i);
            var (periodStart, periodEnd) = CalculateBillingPeriod(new DateTime(targetMonth.Year, targetMonth.Month, 26));

            var monthTransactions = swapTransactions
                .Where(st => st.StartedAt >= periodStart && st.StartedAt <= periodEnd)
                .ToList();

            var swapCount = monthTransactions.Count;
            
            // ✅ Get payment for this period (refactored from invoice)
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.UserSubscriptionId == subscriptionId &&
                                         p.CreatedAt >= periodStart &&
                                         p.CreatedAt <= periodEnd &&
                                         p.Status == Models.PaymentStatus.Completed);

            // ✅ SIMPLIFIED: Usage tier based on swap count
            var maxSwaps = subscription.SubscriptionPlan.MaxSwapsPerMonth;
            var usageTier = maxSwaps.HasValue 
                ? $"{swapCount}/{maxSwaps} lần"
                : $"{swapCount} lần (không giới hạn)";

            monthlyUsage.Add(new MonthlyUsageDto
{
                Year = periodEnd.Year,
                Month = periodEnd.Month,
                MonthName = CultureInfo.GetCultureInfo("vi-VN").DateTimeFormat.GetMonthName(targetMonth.Month),
                SwapCount = swapCount,
                MonthlyFee = payment?.Amount ?? subscription.SubscriptionPlan.MonthlyPrice,
                UsageTier = usageTier,
                IsPaid = payment?.Status == Models.PaymentStatus.Completed
            });
        }

        return monthlyUsage;
    }
}