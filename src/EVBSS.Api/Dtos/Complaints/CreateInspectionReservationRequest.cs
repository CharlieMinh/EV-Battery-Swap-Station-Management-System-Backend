using System;
using System.ComponentModel.DataAnnotations;
// Đã xóa: using EVBSS.Api.Dtos.Reservations; // Không kế thừa từ CreateReservationRequest nữa

namespace EVBSS.Api.Dtos.Complaints;

/// <summary>
/// DTO để Driver đặt lịch hẹn kiểm tra ban đầu cho Complaint đã báo cáo.
/// KHÔNG CẦN VehicleId vì có thể suy ra từ ComplaintId (Complaint -> SwapTransaction -> Vehicle).
/// </summary>
public class CreateInspectionReservationRequest 
{
    [Required]
    public Guid ComplaintId { get; set; }

    /// <summary>
    /// ID của trạm đổi pin
    /// </summary>
    [Required(ErrorMessage = "StationId là bắt buộc")]
    public Guid StationId { get; set; }

    /// <summary>
    /// Ngày đặt lịch (không bao gồm giờ)
    /// VD: 2025-10-25
    /// </summary>
    [Required(ErrorMessage = "SlotDate là bắt buộc")]
    public DateOnly SlotDate { get; set; }
    
    /// <summary>
    /// Giờ bắt đầu slot (VD: 09:00:00)
    /// </summary>
    [Required(ErrorMessage = "SlotStartTime là bắt buộc")]
    public TimeSpan SlotStartTime { get; set; }
    
    /// <summary>
    /// Giờ kết thúc slot (VD: 09:30:00)
    /// </summary>
    [Required(ErrorMessage = "SlotEndTime là bắt buộc")]
    public TimeSpan SlotEndTime { get; set; }
}