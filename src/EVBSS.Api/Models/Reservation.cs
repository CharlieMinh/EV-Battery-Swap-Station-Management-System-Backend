namespace EVBSS.Api.Models;

/// <summary>
/// Reservation với hệ thống slot-based booking
/// </summary>
public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid StationId { get; set; }
    public Guid? VehicleId { get; set; }

    public Guid BatteryModelId { get; set; }
    
    public Guid? BatteryUnitId { get; set; }    /// Pin được assign khi check-in (nullable vì chưa assign ngay khi đặt)
    
    public Guid? PaymentId { get; set; }    /// Payment ID for pay-per-swap bookings (nullable)
    
    public Guid? UserSubscriptionId { get; set; }    /// UserSubscription ID khi đặt lịch bằng gói (nullable)
    public DateOnly SlotDate { get; set; }
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
    public string? QRCode { get; set; }
    public DateTime? CheckedInAt { get; set; }    /// Thời điểm quét QR tại trạm
    public Guid? VerifiedByStaffId { get; set; }    /// Staff nào verify check-in
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;    /// Trạng thái đặt lịch
    public CancelReason? CancelReason { get; set; }    /// Lý do hủy (nếu Status = Cancelled)
    public string? CancelNote { get; set; }    /// Ghi chú thêm về lý do hủy
    public DateTime? CancelledAt { get; set; }    /// Thời điểm hủy

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
    public Guid? RelatedComplaintId { get; set; }    /// Liên kết đến một BatteryComplaint mà khiến đặt lịch này được tạo
    public BatteryComplaint? RelatedComplaint { get; set; }    /// Liên kết đến một BatteryComplaint mà khiến đặt lịch này được tạo

    // Navigation properties
    public User User { get; set; } = null!;
    public Station Station { get; set; } = null!;
    public BatteryModel BatteryModel { get; set; } = null!;
    public BatteryUnit? BatteryUnit { get; set; }
    public User? VerifiedByStaff { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Payment? Payment { get; set; }  //  Link to Payment for pay-per-swap
    public UserSubscription? UserSubscription { get; set; }  //  Link to UserSubscription when using plan
}
