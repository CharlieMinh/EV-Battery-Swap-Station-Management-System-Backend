using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Request DTO để tạo reservation + payment cho pay-per-swap (đặt lịch lẻ, trả tiền theo lần)
/// </summary>
public class CreatePayPerSwapReservationRequest
{
    /// <summary>
    /// ID của trạm đổi pin
    /// </summary>
    [Required(ErrorMessage = "StationId là bắt buộc")]
    public Guid StationId { get; set; }
    
    /// <summary>
    /// ID của loại pin (BatteryModel) tương thích với xe
    /// </summary>
    [Required(ErrorMessage = "BatteryModelId là bắt buộc")]
    public Guid BatteryModelId { get; set; }
    
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
    
    /// <summary>
    /// Số tiền thanh toán (VD: 25000 VND cho 1 lần đổi pin)
    /// </summary>
    [Required(ErrorMessage = "Amount là bắt buộc")]
    [Range(1, double.MaxValue, ErrorMessage = "Amount phải lớn hơn 0")]
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Phương thức thanh toán
    /// 0 = VNPay (online), 1 = Cash (tiền mặt tại trạm)
    /// </summary>
    [Required(ErrorMessage = "PaymentMethod là bắt buộc")]
    public PaymentMethod PaymentMethod { get; set; }
}
