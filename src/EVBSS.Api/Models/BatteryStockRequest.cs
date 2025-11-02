using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Models;

/// <summary>
/// Trạng thái của yêu cầu tăng pin từ Staff
/// </summary>
public enum BatteryStockRequestStatus
{
    PendingAdminReview,  // Chờ Admin duyệt
    Approved,            // Admin đã duyệt
    Rejected,            // Admin từ chối
    Completed            // Staff đã xác nhận nhận pin
}

/// <summary>
/// Yêu cầu tăng pin từ Staff gửi đến Admin
/// </summary>
public class BatteryStockRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Thông tin yêu cầu
    [Required]
    public Guid StationId { get; set; }
    public Station? Station { get; set; }

    [Required]
    public Guid BatteryModelId { get; set; }
    public BatteryModel? BatteryModel { get; set; }

    [Required]
    [Range(1, 100)]
    public int Quantity { get; set; } // Số lượng pin yêu cầu

    [MaxLength(500)]
    public string? StaffNote { get; set; } // Ghi chú của Staff

    // Trạng thái
    public BatteryStockRequestStatus Status { get; set; } = BatteryStockRequestStatus.PendingAdminReview;

    // Thông tin người yêu cầu
    [Required]
    public Guid RequestedByStaffId { get; set; }
    public User? RequestedByStaff { get; set; }

    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Thông tin Admin duyệt
    public Guid? AdminReviewerId { get; set; }
    public User? AdminReviewer { get; set; }

    public DateTime? AdminReviewDate { get; set; }
    
    [MaxLength(500)]
    public string? AdminNote { get; set; } // Lý do từ chối hoặc ghi chú duyệt

    // Liên kết với BulkCreateRequest đã được tạo tự động
    public Guid? RelatedBulkCreateRequestId { get; set; }
    public BulkCreateRequest? RelatedBulkCreateRequest { get; set; }
}
