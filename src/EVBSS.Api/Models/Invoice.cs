namespace EVBSS.Api.Models;

public enum InvoiceType
{
    SubscriptionMonthly = 0,     // Hóa đơn thuê bao hàng tháng
    SwapTransaction = 1,         // Hóa đơn giao dịch đổi pin
    Deposit = 2,                 // Hóa đơn tiền cọc
    OverdueFee = 3,              // Phí phạt trễ hạn
    ExtraKmFee = 4,              // Phí vượt km
    BatteryPurchase = 5,         // Mua đứt pin
    TradeInCredit = 6            // Thu cũ đổi mới
}

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = null!;           // EVB-INV-2025090001
    public Guid UserId { get; set; }
    public Guid? UserSubscriptionId { get; set; }               // Null nếu không liên quan subscription

    // Invoice details
    public InvoiceType Type { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }                        // Hạn thanh toán
    public DateTime? PaidDate { get; set; }

    // VinFast billing period (for subscription invoices)
    public DateTime? BillingPeriodStart { get; set; }            // 26/tháng trước
    public DateTime? BillingPeriodEnd { get; set; }              // 25/tháng hiện tại
    public int? KmUsedInPeriod { get; set; }                     // Km sử dụng trong kỳ

    // Financial details
    public decimal SubtotalAmount { get; set; }                  // Tiền trước thuế
    public decimal TaxAmount { get; set; }                       // Thuế VAT (10%)
    public decimal TotalAmount { get; set; }                     // Tổng tiền
    public decimal PaidAmount { get; set; } = 0;                 // Đã thanh toán
    public decimal RemainingAmount => TotalAmount - PaidAmount;  // Còn nợ

    // Overdue handling (VinFast style)
    public decimal OverdueFeeAmount { get; set; } = 0;           // Phí phạt trễ hạn
    public bool IsOverdue => DueDate < DateTime.UtcNow && RemainingAmount > 0;
    public int DaysOverdue => IsOverdue ? (DateTime.UtcNow - DueDate).Days : 0;

    // Status
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Notes { get; set; }                           // Ghi chú
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public UserSubscription? UserSubscription { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<SwapTransaction> SwapTransactions { get; set; } = new List<SwapTransaction>();
}