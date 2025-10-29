namespace EVBSS.Api.Models;

/// <summary>
/// Reservation với hệ thống slot-based booking
/// </summary>
public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid StationId { get; set; }
    public Guid BatteryModelId { get; set; }
    
    /// <summary>
    /// ⭐ NEW: Vehicle associated with this reservation (useful for matching compatible BatteryModel)
    /// </summary>
    public Guid? VehicleId { get; set; }
    
    /// <summary>
    /// Pin được assign khi check-in (nullable vì chưa assign ngay khi đặt)
    /// </summary>
    public Guid? BatteryUnitId { get; set; }
    
    /// <summary>
    /// ⭐ NEW 2025-10-25: Payment ID for pay-per-swap bookings (nullable)
    /// Nếu null → Đặt lịch bằng subscription (miễn phí)
    /// Nếu != null → Đặt lịch trả theo lượt (pay-per-swap)
    /// </summary>
    public Guid? PaymentId { get; set; }
    
    /// <summary>
    /// ⭐ NEW 2025-10-25: UserSubscription ID khi đặt lịch bằng gói (nullable)
    /// Nếu null → Đặt lịch pay-per-swap
    /// Nếu != null → Đặt lịch bằng subscription
    /// </summary>
    public Guid? UserSubscriptionId { get; set; }

    // === SLOT-BASED BOOKING FIELDS ===
    /// <summary>
    /// Ngày đặt lịch (chỉ ngày, không có giờ)
    /// UPDATED: Changed from DateTime to DateOnly to avoid timezone issues
    /// </summary>
    public DateOnly SlotDate { get; set; }
    
    /// <summary>
    /// Giờ bắt đầu slot (VD: 09:00:00)
    /// </summary>
    public TimeSpan SlotStartTime { get; set; }
    
    /// <summary>
    /// Giờ kết thúc slot (VD: 09:30:00)
    /// </summary>
    public TimeSpan SlotEndTime { get; set; }

    // === QR CODE & CHECK-IN FIELDS ===
    /// <summary>
    /// Mã QR để check-in tại trạm (Base64 encoded)
    /// </summary>
    public string? QRCode { get; set; }
    
    /// <summary>
    /// Thời điểm quét QR tại trạm
    /// </summary>
    public DateTime? CheckedInAt { get; set; }
    
    /// <summary>
    /// Staff nào verify check-in
    /// </summary>
    public Guid? VerifiedByStaffId { get; set; }

    // === STATUS & CANCELLATION ===
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    
    /// <summary>
    /// Lý do hủy (nếu Status = Cancelled)
    /// </summary>
    public CancelReason? CancelReason { get; set; }
    
    /// <summary>
    /// Ghi chú thêm về lý do hủy
    /// </summary>
    public string? CancelNote { get; set; }
    
    /// <summary>
    /// Thời điểm hủy
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ⭐ NEW: Link back to a BatteryComplaint that triggered this re-swap reservation
    public Guid? RelatedComplaintId { get; set; }
    public BatteryComplaint? RelatedComplaint { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Station Station { get; set; } = null!;
    public BatteryModel BatteryModel { get; set; } = null!;
    public BatteryUnit? BatteryUnit { get; set; }
    public User? VerifiedByStaff { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Payment? Payment { get; set; }  // ⭐ Link to Payment for pay-per-swap
    public UserSubscription? UserSubscription { get; set; }  // ⭐ Link to UserSubscription when using plan
}
