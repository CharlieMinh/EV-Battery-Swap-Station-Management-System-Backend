namespace EVBSS.Api.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PaymentReference { get; set; } = null!;        // Mã giao dịch (VNPay, etc.)
    
    // ✅ REFACTORED: Link to either Subscription OR Reservation (mutual exclusive)
    public Guid? UserSubscriptionId { get; set; }                // Thanh toán cho gói subscription (LUỒNG 1)
    public Guid? ReservationId { get; set; }                     // Thanh toán cho đặt lịch lẻ (LUỒNG 2 - Pay-per-Swap)
    public Guid UserId { get; set; }

    // Payment details
    public PaymentMethod Method { get; set; }
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    
    public string Description { get; set; } = null!;             // Mô tả thanh toán

    // VNPay integration fields (for security & audit)
    public string? VnpTxnRef { get; set; }                       // Mã giao dịch VNPay
    public string? VnpTransactionNo { get; set; }                // Mã GD tại VNPay
    public string? VnpResponseCode { get; set; }                 // Mã phản hồi (00=success, 24=cancelled, 51=insufficient)
    public string? VnpSecureHash { get; set; }                   // Chữ ký điện tử (verify callback)

    // Cash payment fields (for staff)
    public Guid? ProcessedByStaffId { get; set; }                // Staff xử lý thanh toán
    public Guid? StationId { get; set; }                         // Trạm thanh toán

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;   // Thời điểm user BẮT ĐẦU thanh toán
    public DateTime? CompletedAt { get; set; }                   // Thời điểm HOÀN TẤT thanh toán

    // Navigation properties
    public UserSubscription? UserSubscription { get; set; }
    public Reservation? Reservation { get; set; }                // Link to Reservation for pay-per-swap
    public User User { get; set; } = null!;
    public User? ProcessedByStaff { get; set; }
    public Station? Station { get; set; }
}