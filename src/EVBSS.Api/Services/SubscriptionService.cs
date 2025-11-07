using EVBSS.Api.Configuration;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Subscriptions;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EVBSS.Api.Services;

public interface ISubscriptionService
{
    Task<SubscriptionCreatedResponse> CreateSubscriptionAsync(Guid userId, CreateSubscriptionRequest request);

    /// <summary>
    /// Tạo subscription pending (chờ thanh toán) theo flow Frontend yêu cầu
    /// Tạo UserSubscription với IsActive=false + Payment pending + VNPay URL
    /// </summary>
    Task<CreatePendingSubscriptionResponse> CreatePendingSubscriptionAsync(Guid userId, CreatePendingSubscriptionRequest request, string ipAddress);

    Task<UserSubscriptionDto?> GetUserActiveSubscriptionAsync(Guid userId);

    /// <summary>
    /// Lấy tất cả subscriptions của user (bao gồm cả active và inactive)
    /// </summary>
    Task<IEnumerable<UserSubscriptionDto>> GetUserAllSubscriptionsAsync(Guid userId);

    Task<CancelSubscriptionResponse> CancelSubscriptionAsync(Guid userId);
    Task<SubscriptionUsageDto?> GetSubscriptionUsageAsync(Guid userId);
    Task CheckAndExpireSubscriptionsAsync(); // ⭐ NEW: Auto-expire logic
}

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly VnPayConfig _vnPayConfig;
    private readonly IVnPayServiceV2 _vnPayServiceV2;

    public SubscriptionService(
        AppDbContext context,
        ILogger<SubscriptionService> logger,
        IOptions<VnPayConfig> vnPayConfig,
        IVnPayServiceV2 vnPayServiceV2)
    {
        _context = context;
        _logger = logger;
        _vnPayConfig = vnPayConfig.Value;
        _vnPayServiceV2 = vnPayServiceV2;
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
        // Check if user already has active subscription for any vehicle
        var existingSubscriptions = await _context.UserSubscriptions
            .Where(us => us.UserId == userId
                         && us.IsActive
                         && us.VehicleId.HasValue
                         && request.VehicleIds.Contains(us.VehicleId.Value))
            .ToListAsync();

        if (existingSubscriptions.Any())
        {
            throw new InvalidOperationException("Một hoặc nhiều xe đã có gói subscription đang hoạt động. Vui lòng hủy gói hiện tại trước khi đăng ký mới.");
        }

        // ⭐ FIX 2025-10-25: Check if any vehicle has pending payment (prevent spam subscription creation)
        var pendingPayments = await _context.Payments
            .Include(p => p.UserSubscription)
            .Where(p => p.UserId == userId
                     && p.UserSubscription != null
                     && p.UserSubscription.VehicleId.HasValue
                     && request.VehicleIds.Contains(p.UserSubscription.VehicleId.Value)
                     && p.Status == PaymentStatus.Pending
                     && p.Type == PaymentType.Subscription)
            .ToListAsync();

        if (pendingPayments.Any())
        {
            throw new InvalidOperationException($"Một hoặc nhiều xe đã có gói subscription đang chờ thanh toán. Vui lòng hoàn tất thanh toán hoặc hủy gói cũ trước khi mua gói mới.");
        }

        // Validate subscription plan exists and is active
        var subscriptionPlan = await _context.SubscriptionPlans
            .Include(sp => sp.BatteryModel)
            .FirstOrDefaultAsync(sp => sp.Id == request.SubscriptionPlanId && sp.IsActive);

        if (subscriptionPlan == null)
        {
            throw new ArgumentException("Gói subscription không tồn tại hoặc đã bị vô hiệu hóa.");
        }

        // Validate all vehicles belong to user and are compatible
        var vehicles = await _context.Vehicles
            .Include(v => v.CompatibleModel)
            .Where(v => request.VehicleIds.Contains(v.Id) && v.UserId == userId)
            .ToListAsync();

        if (vehicles.Count != request.VehicleIds.Count)
        {
            throw new ArgumentException("Một hoặc nhiều xe không tồn tại hoặc không thuộc về bạn.");
        }

        foreach (var vehicle in vehicles)
        {
            if (vehicle.CompatibleBatteryModelId != subscriptionPlan.BatteryModelId)
            {
                throw new InvalidOperationException($"Xe {vehicle.Plate} không tương thích với gói pin {subscriptionPlan.Name}.");
            }
        }

        // ✅ SIMPLIFIED: 30-day billing period (from start date)
        var startDate = request.StartDate ?? DateTime.UtcNow;
        var billingStart = startDate;
        var billingEnd = startDate.AddDays(30);  // 30 days from now

        var subscriptions = new List<UserSubscription>();
        foreach (var vehicle in vehicles)
        {
            var subscription = new UserSubscription
            {
                UserId = userId,
                SubscriptionPlanId = request.SubscriptionPlanId,
                VehicleId = vehicle.Id,
                StartDate = startDate,
                CurrentBillingPeriodStart = billingStart,
                CurrentBillingPeriodEnd = billingEnd,
                CurrentMonthSwapCount = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            subscriptions.Add(subscription);
            _context.UserSubscriptions.Add(subscription);
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} created subscription {SubscriptionId} for vehicles [{VehicleIds}], billing {Start} to {End}",
            userId, string.Join(",", subscriptions.Select(s => s.Id)), string.Join(",", request.VehicleIds), billingStart.ToString("yyyy-MM-dd"), billingEnd.ToString("yyyy-MM-dd"));

        return new SubscriptionCreatedResponse
        {
            SubscriptionId = subscriptions.First().Id,
            Message = $"Đăng ký gói {subscriptionPlan.Name} thành công cho {vehicles.Count} xe!",
            MonthlyPrice = subscriptionPlan.MonthlyPrice,
            MaxSwapsPerMonth = subscriptionPlan.MaxSwapsPerMonth,
            StartDate = startDate,
            BillingPeriodStart = billingStart,
            BillingPeriodEnd = billingEnd
        };
    }

    /// <summary>
    /// Tạo subscription pending (chờ thanh toán) - FLOW FRONTEND YÊU CẦU
    /// ⭐ FIXED 2025-10-25: Allow multiple subscriptions for different vehicles
    /// 1. Tạo UserSubscription với IsActive = FALSE
    /// 2. Tạo Payment với Status = Pending
    /// 3. Generate VNPay payment URL
    /// 4. Return tất cả thông tin cần thiết cho FE
    /// </summary>
    public async Task<CreatePendingSubscriptionResponse> CreatePendingSubscriptionAsync(
        Guid userId,
        CreatePendingSubscriptionRequest request,
        string ipAddress)
    {
        // 1. Validate subscription plan exists and is active
        var subscriptionPlan = await _context.SubscriptionPlans
            .Include(sp => sp.BatteryModel)
            .FirstOrDefaultAsync(sp => sp.Id == request.SubscriptionPlanId && sp.IsActive);

        if (subscriptionPlan == null)
        {
            throw new ArgumentException("Gói subscription không tồn tại hoặc đã bị vô hiệu hóa.");
        }
        // 2. Enforce: one active/pending subscription per BatteryModel per user
        var batteryModelId = subscriptionPlan.BatteryModelId;

        // 2.a Active subscription exists on same BatteryModel?
        var hasActiveSameModel = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .AnyAsync(us => us.UserId == userId
                           && us.IsActive
                           && us.SubscriptionPlan.BatteryModelId == batteryModelId);

        if (hasActiveSameModel)
        {
            throw new InvalidOperationException("Bạn đã có gói đang hoạt động cho cùng loại pin. Vui lòng hủy hoặc chờ hết hạn trước khi mua gói mới.");
        }

        // 2.b Pending payment for same BatteryModel?
        var hasPendingPaymentSameModel = await _context.Payments
            .Include(p => p.UserSubscription)!
                .ThenInclude(us => us!.SubscriptionPlan)
            .AnyAsync(p => p.UserId == userId
                           && p.Status == PaymentStatus.Pending
                           && p.Type == PaymentType.Subscription
                           && p.UserSubscription != null
                           && p.UserSubscription.SubscriptionPlan.BatteryModelId == batteryModelId);

        if (hasPendingPaymentSameModel)
        {
            throw new InvalidOperationException("Bạn đang có một gói chờ thanh toán cho cùng loại pin. Vui lòng hoàn tất hoặc hủy trước khi tạo gói mới.");
        }

        // 3. ⭐ Tạo UserSubscription với IsActive = FALSE (chờ thanh toán) và không gắn Vehicle
        var subscription = new UserSubscription
        {
            UserId = userId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            VehicleId = null,
            IsActive = false,  // ⭐ QUAN TRỌNG: Chưa kích hoạt
            StartDate = null,  // ⭐ NULL = chưa kích hoạt, sẽ set khi thanh toán thành công
            CurrentBillingPeriodStart = DateTime.MinValue,
            CurrentBillingPeriodEnd = DateTime.MinValue,
            CurrentMonthSwapCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.UserSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // 5. ⭐ Tạo Payment record với Status = Pending
        var payment = new Payment
        {
            UserSubscriptionId = subscription.Id,
            UserId = userId,
            Method = PaymentMethod.VNPay,  // Default VNPay, user có thể đổi sang Cash sau
            Type = PaymentType.Subscription,
            Amount = subscriptionPlan.MonthlyPrice,
            Status = PaymentStatus.Pending,
            VnpTxnRef = GenerateTransactionReference(),
            PaymentReference = GenerateTransactionReference(),
            Description = $"Thanh toán gói {subscriptionPlan.Name} (Pin: {subscriptionPlan.BatteryModel.Name})",
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // 6. ⭐ Generate VNPay payment URL
        var paymentUrl = GenerateVnPayUrl(payment, subscription, subscriptionPlan, null, ipAddress);

        _logger.LogInformation("User {UserId} created PENDING subscription {SubscriptionId}, payment {PaymentId}",
            userId, subscription.Id, payment.Id);

        // 7. Return full response with all info FE needs
        return new CreatePendingSubscriptionResponse
        {
            PaymentId = payment.Id,
            UserSubscriptionId = subscription.Id,
            PaymentUrl = paymentUrl,
            Amount = subscriptionPlan.MonthlyPrice,
            PlanName = subscriptionPlan.Name,
            PlanDescription = subscriptionPlan.Description,
            MaxSwapsPerMonth = subscriptionPlan.MaxSwapsPerMonth ?? 0,
            Message = "Gói subscription đã được tạo. Vui lòng chọn phương thức thanh toán."
        };
    }

    public async Task<UserSubscriptionDto?> GetUserActiveSubscriptionAsync(Guid userId)
    {
        var subscriptions = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
                .ThenInclude(sp => sp.BatteryModel)
            .Include(us => us.Vehicle)
            .Where(us => us.UserId == userId && us.IsActive)
            .ToListAsync();

        if (!subscriptions.Any())
            return null;

        var firstSub = subscriptions.First();
        return new UserSubscriptionDto
        {
            Id = firstSub.Id,
            UserId = firstSub.UserId,
            SubscriptionPlanId = firstSub.SubscriptionPlanId,
            VehicleIds = subscriptions.Where(s => s.VehicleId.HasValue).Select(s => s.VehicleId!.Value).ToList(),
            StartDate = firstSub.StartDate,
            EndDate = firstSub.EndDate,
            IsActive = firstSub.IsActive,
            CurrentBillingPeriodStart = firstSub.CurrentBillingPeriodStart,
            CurrentBillingPeriodEnd = firstSub.CurrentBillingPeriodEnd,
            CurrentMonthSwapCount = firstSub.CurrentMonthSwapCount,
            LastPaymentDate = firstSub.LastPaymentDate,
            CreatedAt = firstSub.CreatedAt,
            SubscriptionPlan = new SubscriptionPlanDto
            {
                Id = firstSub.SubscriptionPlan.Id,
                Name = firstSub.SubscriptionPlan.Name,
                Description = firstSub.SubscriptionPlan.Description,
                MonthlyPrice = firstSub.SubscriptionPlan.MonthlyPrice,
                MaxSwapsPerMonth = firstSub.SubscriptionPlan.MaxSwapsPerMonth,
                Benefits = firstSub.SubscriptionPlan.Benefits,
                RefundPolicy = firstSub.SubscriptionPlan.RefundPolicy,
                BatteryModelId = firstSub.SubscriptionPlan.BatteryModelId,
                BatteryModelName = firstSub.SubscriptionPlan.BatteryModel.Name,
                IsActive = firstSub.SubscriptionPlan.IsActive
            },
            Vehicles = subscriptions
                .Where(s => s.Vehicle != null)
                .Select(s => new SubscriptionVehicleDto
                {
                    Id = s.Vehicle!.Id,
                    Brand = "VinFast",
                    Model = "Unknown",
                    VIN = s.Vehicle!.VIN,
                    Plate = s.Vehicle!.Plate,
                    Color = "Unknown",
                    Year = DateTime.UtcNow.Year
                }).ToList()
        };
    }

    /// <summary>
    /// Lấy tất cả subscriptions của user (bao gồm cả active và inactive)
    /// Dùng cho trang lịch sử subscription
    /// </summary>
    public async Task<IEnumerable<UserSubscriptionDto>> GetUserAllSubscriptionsAsync(Guid userId)
    {
        var subscriptions = await _context.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
                .ThenInclude(sp => sp.BatteryModel)
            .Include(us => us.Vehicle)
            .Where(us => us.UserId == userId)
            .OrderByDescending(us => us.CreatedAt)  // Mới nhất lên đầu
            .ToListAsync();

        if (!subscriptions.Any())
            return new List<UserSubscriptionDto>();

        // Map mỗi subscription thành DTO
        var result = subscriptions.Select(sub => new UserSubscriptionDto
        {
            Id = sub.Id,
            UserId = sub.UserId,
            SubscriptionPlanId = sub.SubscriptionPlanId,
            VehicleIds = sub.VehicleId.HasValue ? new List<Guid> { sub.VehicleId.Value } : new List<Guid>(),
            VehicleId = sub.VehicleId ?? Guid.Empty,
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            IsActive = sub.IsActive,
            CurrentBillingPeriodStart = sub.CurrentBillingPeriodStart,
            CurrentBillingPeriodEnd = sub.CurrentBillingPeriodEnd,
            CurrentMonthSwapCount = sub.CurrentMonthSwapCount,
            LastPaymentDate = sub.LastPaymentDate,
            CreatedAt = sub.CreatedAt,
            SubscriptionPlan = new SubscriptionPlanDto
            {
                Id = sub.SubscriptionPlan.Id,
                Name = sub.SubscriptionPlan.Name,
                Description = sub.SubscriptionPlan.Description,
                MonthlyPrice = sub.SubscriptionPlan.MonthlyPrice,
                MaxSwapsPerMonth = sub.SubscriptionPlan.MaxSwapsPerMonth,
                Benefits = sub.SubscriptionPlan.Benefits,
                RefundPolicy = sub.SubscriptionPlan.RefundPolicy,
                BatteryModelId = sub.SubscriptionPlan.BatteryModelId,
                BatteryModelName = sub.SubscriptionPlan.BatteryModel.Name,
                IsActive = sub.SubscriptionPlan.IsActive
            },
            Vehicles = sub.Vehicle != null
                ? new List<SubscriptionVehicleDto>
                {
                    new SubscriptionVehicleDto
                    {
                        Id = sub.Vehicle.Id,
                        Brand = "VinFast",
                        Model = "Unknown",
                        VIN = sub.Vehicle.VIN,
                        Plate = sub.Vehicle.Plate,
                        Color = "Unknown",
                        Year = DateTime.UtcNow.Year
                    }
                }
                : new List<SubscriptionVehicleDto>()
        }).ToList();

        return result;
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
                Message = "Không tìm thấy gói dịch vụ đang hoạt động."
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

        return new CancelSubscriptionResponse
        {
            Success = true,
            Message = "Hủy gói dịch vụ thành công!",
            EndDate = subscription.EndDate,
            DepositRefund = null  // Không còn deposit
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
        // ⭐ CHỈ đếm swap từ subscription (có Reservation.UserSubscriptionId == subscription.Id)
        // ⭐ KHÔNG đếm pay-per-swap (có Reservation.Payment != null)
        var swapTransactions = await _context.SwapTransactions
            .Include(st => st.Reservation)
            .Where(st =>
                st.UserSubscriptionId == subscription.Id &&
                st.Reservation != null &&
                st.Reservation.Payment == null)  // ← CHỈ swap từ subscription, không phải pay-per-swap
            .OrderBy(st => st.StartedAt)
            .ToListAsync();

        // ✅ Calculate total amount paid from Payments table
        var totalAmountPaid = await _context.Payments
            .Where(p => p.UserSubscriptionId == subscription.Id && p.Status == Models.PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        // ⭐ ĐẾM LẠI swap count từ transactions thực tế trong billing period hiện tại
        // Thay vì dùng CurrentMonthSwapCount từ DB (có thể sai do bug cũ)
        var actualCurrentMonthSwapCount = swapTransactions
            .Count(st =>
                st.StartedAt >= subscription.CurrentBillingPeriodStart &&
                st.StartedAt <= subscription.CurrentBillingPeriodEnd);

        // ✅ FIXED PRICE - No tier calculation needed
        var currentMonthFee = subscription.SubscriptionPlan.MonthlyPrice;
        var plan = subscription.SubscriptionPlan;
        var usageTier = plan.MaxSwapsPerMonth.HasValue
            ? $"{actualCurrentMonthSwapCount}/{plan.MaxSwapsPerMonth} lần"
            : $"{actualCurrentMonthSwapCount} lần (không giới hạn)";

        // Calculate monthly breakdown for last 6 months
        var monthlyUsage = await CalculateMonthlyUsageAsync(subscription.Id, swapTransactions);

        return new SubscriptionUsageDto
        {
            SubscriptionId = subscription.Id,
            SubscriptionPlanName = subscription.SubscriptionPlan.Name,
            VehiclePlate = subscription.Vehicle?.Plate ?? "N/A",
            CurrentBillingPeriodStart = subscription.CurrentBillingPeriodStart,
            CurrentBillingPeriodEnd = subscription.CurrentBillingPeriodEnd,

            // ✅ SIMPLIFIED: Swap count instead of km - Dùng giá trị đếm lại từ transactions
            CurrentMonthSwapCount = actualCurrentMonthSwapCount,
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

    // ========== HELPER METHODS FOR VNPAY URL GENERATION ==========

    private string GenerateTransactionReference()
    {
        return $"EVB{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }

    private string GenerateVnPayUrl(Payment payment, UserSubscription subscription, SubscriptionPlan plan, Vehicle? vehicle, string ipAddress)
    {
        // ⭐ SỬ DỤNG VnPayServiceV2 (theo hướng dẫn chính thức VNPay)
        var paymentModel = new PaymentInformationModel
        {
            OrderType = "billpayment", // subscription payment
            Amount = (double)payment.Amount,
            OrderDescription = $"Thanh toan {plan.Name}",
            Name = vehicle?.Plate ?? plan.BatteryModel.Name
        };

        // Create fake HttpContext with IP address
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);

        var paymentUrl = _vnPayServiceV2.CreatePaymentUrl(paymentModel, httpContext);

        _logger.LogInformation("Generated VNPay URL for payment {PaymentId}: {Url}", payment.Id, paymentUrl);

        return paymentUrl;
    }

    private string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hashBytes).ToLower();
    }
}