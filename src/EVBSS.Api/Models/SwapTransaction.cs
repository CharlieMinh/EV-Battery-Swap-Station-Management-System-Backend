namespace EVBSS.Api.Models;

public enum SwapTransactionStatus
{
    Reserved = 0,                // Đã đặt trước (từ Reservation)
    CheckedIn = 1,              // Khách hàng đã check-in
    BatteryIssued = 2,          // Pin đã cấp cho khách
    BatteryReturned = 3,        // Pin cũ đã trả lại
    Completed = 4,              // Hoàn thành
    Cancelled = 5,              // Đã hủy
    Failed = 6                  // Thất bại
}

public class SwapTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransactionNumber { get; set; } = null!;       /// Mã giao dịch (EVB-SWT-YYYYMMDD####)
    
    // Related entities
    public Guid UserId { get; set; }
    public Guid? ReservationId { get; set; }                     // Từ reservation (có thể null nếu walk-in)
    public Guid StationId { get; set; }                          // Trạm đổi pin
    public Guid VehicleId { get; set; }                          // Xe đổi pin
    public Guid? UserSubscriptionId { get; set; }               // Gói thuê đang sử dụng

    // Battery swap details
    public Guid IssuedBatteryId { get; set; }                    // Pin cấp cho khách
    public Guid? ReturnedBatteryId { get; set; }                 // Pin khách trả lại 
    public string IssuedBatterySerial { get; set; } = null!;     // Serial pin cấp
    public string? ReturnedBatterySerial { get; set; }           // Serial pin trả

    // Staff operations
    public Guid? CheckedInByStaffId { get; set; }                // Staff check-in
    public Guid? BatteryIssuedByStaffId { get; set; }            // Staff cấp pin
    public Guid? BatteryReceivedByStaffId { get; set; }          // Staff nhận pin cũ
    public Guid? CompletedByStaffId { get; set; }                // Staff hoàn thành

    // Optional technical tracking (health only)
    public int? BatteryHealthIssued { get; set; }                // % sức khỏe pin cấp
    public int? BatteryHealthReturned { get; set; }              // % sức khỏe pin trả
    // NOTE: ParentComplaintId removed; use RelatedComplaintId instead to avoid duplication
    
    // ⭐ THÊM MỚI: Liên kết đến khiếu nại pin lỗi đã kích hoạt Re-swap này
    public Guid? RelatedComplaintId { get; set; }

    // Navigation property
    public BatteryComplaint? RelatedComplaint { get; set; }
    // Pricing & Payment
    public PaymentType PaymentType { get; set; }
    public decimal SwapFee { get; set; } = 0;                    // Phí đổi pin (nếu trả theo lần)
    public decimal TotalAmount { get; set; } = 0;                // Tổng phí
    public bool IsPaid { get; set; } = false;

    // Status & Timestamps
    public SwapTransactionStatus Status { get; set; } = SwapTransactionStatus.Reserved;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;   // Bắt đầu giao dịch
    public DateTime? CheckedInAt { get; set; }                   // Thời gian check-in
    public DateTime? BatteryIssuedAt { get; set; }               // Thời gian cấp pin
    public DateTime? BatteryReturnedAt { get; set; }             // Thời gian trả pin
    public DateTime? CompletedAt { get; set; }                   // Hoàn thành
    public DateTime? CancelledAt { get; set; }                   // Hủy bỏ

    // Additional info
    public string? Notes { get; set; }                           // Ghi chú
    public string? CancellationReason { get; set; }              // Lý do hủy
    
    // Feedback và đánh giá từ khách hàng
    public int? Rating { get; set; }                             // Đánh giá 1-5 sao
    public string? Feedback { get; set; }                        // Phản hồi chi tiết
    public DateTime? RatedAt { get; set; }                       // Thời gian đánh giá

    // Navigation properties
    public User User { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    public Station Station { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public UserSubscription? UserSubscription { get; set; }
    
    public BatteryUnit IssuedBattery { get; set; } = null!;
    public BatteryUnit? ReturnedBattery { get; set; }
    
    public User? CheckedInByStaff { get; set; }
    public User? BatteryIssuedByStaff { get; set; }
    public User? BatteryReceivedByStaff { get; set; }
    public User? CompletedByStaff { get; set; }
}