using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVBSS.Api.Models;

public enum ComplaintStatus
{
    // Cập nhật Flow mới:
    
    // 1. Sau khi Driver báo cáo
    PendingScheduling = 0,      // Driver báo cáo, chờ Driver đặt lịch kiểm tra Pin

    // 2. Sau khi Driver đặt lịch
    Scheduled = 1,              // Đã đặt lịch kiểm tra Pin, chờ tới ngày hẹn
    
    // 3. Staff Check-in tại trạm
    CheckedIn = 2,              // Staff đã Check-in Pin, Pin đã ở trạm, chờ bắt đầu kiểm tra

    // 4. Staff bắt đầu quá trình kiểm tra
    Investigating = 3,          // Đang trong quá trình kiểm tra thực tế (Staff đang thực hiện)
    
    // 5. Staff ra quyết định
    Confirmed = 4,              // Xác nhận lỗi Pin do Hệ thống/Bảo hành, CHỜ Staff thực hiện Finalize Reswap
    Rejected = 5,               // Bị từ chối (Lỗi do người dùng/Pin ngoại lai) -> Complaint đóng

    // 6. Reswap thành công (chỉ áp dụng sau Confirmed)
    Resolved = 6                // Đã giải quyết xong (Đã cấp Re-swap thành công) -> Complaint đóng
}

public class BatteryComplaint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SwapTransactionId { get; set; } // Giao dịch đổi pin mà pin lỗi được cấp

    [Required]
    public Guid IssuedBatteryId { get; set; } // Pin cụ thể bị báo lỗi
    
    [Required]
    public Guid ReportedByUserId { get; set; }

    [Required]
    // Trạng thái mặc định được cập nhật
    public ComplaintStatus Status { get; set; } = ComplaintStatus.PendingScheduling;

    [MaxLength(500)]
    public string ComplaintDetails { get; set; } = null!; // Mô tả của Driver

    public DateTime ReportDate { get; set; } = DateTime.UtcNow;

    // Thông tin xử lý (Audit Trail)
    public Guid? HandledByStaffId { get; set; }
    public string? ResolutionNotes { get; set; } // Ghi chú của Staff về quyết định và nguyên nhân
    public DateTime? ResolvedAt { get; set; }
    
    // Navigation properties
    public SwapTransaction? SwapTransaction { get; set; }
    public BatteryUnit? IssuedBattery { get; set; }
    public User? ReportedByUser { get; set; }
    public User? HandledByStaff { get; set; }
}