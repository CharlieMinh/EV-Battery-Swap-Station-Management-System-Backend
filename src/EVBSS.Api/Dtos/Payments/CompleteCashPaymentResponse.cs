namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Response khi Staff xác nhận thanh toán tiền mặt thành công
/// Bao gồm đầy đủ thông tin về người thanh toán, gói dịch vụ, xe, và staff xử lý
/// </summary>
public class CompleteCashPaymentResponse
{
    public bool Success { get; set; }
    public Guid PaymentId { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
    
    // ⭐ Thông tin chi tiết thanh toán
    public PaymentDetailInfo? PaymentDetail { get; set; }
}

/// <summary>
/// Thông tin chi tiết về thanh toán
/// </summary>
public class PaymentDetailInfo
{
    // Thông tin thanh toán
    public decimal Amount { get; set; }
    public string Method { get; set; } = default!; // "Cash", "VNPay"
    public string Type { get; set; } = default!; // "Subscription", "PayPerSwap"
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Description { get; set; }
    
    // Thông tin người thanh toán (Customer/Driver)
    public UserInfo User { get; set; } = default!;
    
    // Thông tin gói dịch vụ (nếu là Subscription)
    public SubscriptionPlanInfo? SubscriptionPlan { get; set; }
    
    // Thông tin xe (nếu có)
    public VehicleInfo? Vehicle { get; set; }
    
    // Thông tin đặt lịch (nếu là Pay-per-Swap)
    public ReservationInfo? Reservation { get; set; }
    
    // Thông tin staff xử lý
    public StaffInfo? ProcessedByStaff { get; set; }
    
    // Thông tin trạm
    public StationInfo? Station { get; set; }
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PhoneNumber { get; set; }
}

public class SubscriptionPlanInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal MonthlyPrice { get; set; }
    public int MaxSwapsPerMonth { get; set; }
    public string BatteryModelName { get; set; } = default!;
}

public class VehicleInfo
{
    public Guid Id { get; set; }
    public string Plate { get; set; } = default!;
    public string? VIN { get; set; }
    public string? VehicleModelName { get; set; }
}

public class ReservationInfo
{
    public Guid Id { get; set; }
    public DateOnly SlotDate { get; set; }
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
    public string Status { get; set; } = default!;
}

public class StaffInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
}

public class StationInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Address { get; set; } = default!;
}
