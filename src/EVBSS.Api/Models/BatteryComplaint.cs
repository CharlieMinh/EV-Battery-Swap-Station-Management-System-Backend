// File mới: src/EVBSS.Api/Models/BatteryComplaint.cs

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVBSS.Api.Models;

public enum ComplaintStatus
{
    Pending = 0,      // Driver báo cáo, chờ Staff xác minh
    Investigating = 1,  // Đang kiểm tra thực tế (có thể bỏ qua hoặc dùng tạm)
    Confirmed = 2,      // Xác nhận lỗi Pin do Hệ thống/Bảo hành (System Fault)
    Rejected = 3,       // Từ chối (Lỗi do người dùng/Pin ngoại lai)
    Resolved = 4        // Đã giải quyết xong (Đã cấp Re-swap/Áp dụng phí phạt)
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
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Pending;

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