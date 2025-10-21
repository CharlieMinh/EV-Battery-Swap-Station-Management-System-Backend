namespace EVBSS.Api.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PaymentReference { get; set; } = null!;        // Mã giao dịch (VNPay, etc.)
    
    // ✅ REFACTORED: Link to Subscription instead of Invoice
    public Guid? UserSubscriptionId { get; set; }                // Thanh toán cho gói subscription
    public Guid UserId { get; set; }

    // Payment details
    public PaymentMethod Method { get; set; }
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    
    public string Description { get; set; } = null!;             // Mô tả thanh toán

    // VNPay integration fields
    public string? VnpTxnRef { get; set; }                       // Mã giao dịch VNPay
    public string? VnpTransactionNo { get; set; }                // Mã GD tại VNPay
    public string? VnpResponseCode { get; set; }                 // Mã phản hồi
    public string? VnpSecureHash { get; set; }                   // Chữ ký điện tử
    public DateTime? VnpPayDate { get; set; }                    // Thời gian thanh toán VNPay

    // Cash payment fields (for staff)
    public Guid? ProcessedByStaffId { get; set; }                // Staff xử lý thanh toán
    public Guid? StationId { get; set; }                         // Trạm thanh toán

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Additional info
    public string? Notes { get; set; }
    public string? FailureReason { get; set; }                   // Lý do thất bại

    // Navigation properties
    public UserSubscription? UserSubscription { get; set; }
    public User User { get; set; } = null!;
    public User? ProcessedByStaff { get; set; }
    public Station? Station { get; set; }
}