namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Response DTO cho danh sách payment (Staff/Admin dashboard)
/// </summary>
public class PaymentListResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPhone { get; set; }
    
    /// <summary>
    /// ID của UserSubscription (nếu thanh toán gói)
    /// </summary>
    public Guid? UserSubscriptionId { get; set; }
    
    /// <summary>
    /// Tên gói subscription (nếu có)
    /// </summary>
    public string? SubscriptionPlanName { get; set; }
    
    /// <summary>
    /// ID của Reservation (nếu thanh toán đặt lịch lẻ)
    /// </summary>
    public Guid? ReservationId { get; set; }
    
    /// <summary>
    /// Phương thức thanh toán: VNPay, Cash, BankTransfer, Momo
    /// </summary>
    public string Method { get; set; } = string.Empty;
    
    /// <summary>
    /// Loại thanh toán: Subscription (gói), PayPerSwap (lẻ), BuyOutright, TradeIn
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Số tiền
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Trạng thái: Pending, Processing, Completed, Failed, Cancelled, Refunded
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả giao dịch
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Mã giao dịch VNPay
    /// </summary>
    public string? VnpTxnRef { get; set; }
    
    /// <summary>
    /// Mã tham chiếu nội bộ
    /// </summary>
    public string? PaymentReference { get; set; }
    
    /// <summary>
    /// Mã response từ VNPay (00 = success)
    /// </summary>
    public string? VnpResponseCode { get; set; }
    
    /// <summary>
    /// Mã giao dịch từ VNPay
    /// </summary>
    public string? VnpTransactionNo { get; set; }
    
    /// <summary>
    /// Thời gian tạo
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Thời gian hoàn thành (nếu có)
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// ID của staff xử lý (nếu thanh toán cash)
    /// </summary>
    public Guid? ProcessedByStaffId { get; set; }
    
    /// <summary>
    /// Tên staff xử lý
    /// </summary>
    public string? ProcessedByStaffName { get; set; }
}

/// <summary>
/// Response DTO cho chi tiết payment
/// </summary>
public class PaymentDetailResponse : PaymentListResponse
{
    /// <summary>
    /// Thông tin user đầy đủ
    /// </summary>
    public PaymentUserInfo? User { get; set; }
    
    /// <summary>
    /// Thông tin subscription (nếu có)
    /// </summary>
    public PaymentSubscriptionInfo? Subscription { get; set; }
    
    /// <summary>
    /// Thông tin reservation (nếu có)
    /// </summary>
    public PaymentReservationInfo? Reservation { get; set; }
}

public class PaymentUserInfo
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class PaymentSubscriptionInfo
{
    public Guid UserSubscriptionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int? MaxSwapsPerMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class PaymentReservationInfo
{
    public Guid ReservationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public DateOnly SlotDate { get; set; }
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}
