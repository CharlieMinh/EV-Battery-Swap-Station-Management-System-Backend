using System;
using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Dtos.Reservations; // Reuse fields from CreateReservationRequest

namespace EVBSS.Api.Dtos.Complaints;

/// <summary>
/// DTO để Driver đặt lịch hẹn kiểm tra ban đầu cho Complaint đã báo cáo.
/// Kế thừa từ CreateReservationRequest
/// </summary>
public class CreateInspectionReservationRequest : CreateReservationRequest
{
    [Required]
    public Guid ComplaintId { get; set; }
}
