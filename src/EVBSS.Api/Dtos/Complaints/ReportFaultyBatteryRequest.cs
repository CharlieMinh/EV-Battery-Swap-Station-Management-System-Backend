using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Complaints;

public class ReportFaultyBatteryRequest
{
    [Required(ErrorMessage = "Swap Transaction ID là bắt buộc")]
    public Guid SwapTransactionId { get; set; }

    [Required(ErrorMessage = "Mô tả lỗi là bắt buộc")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Mô tả lỗi phải từ 10 đến 500 ký tự")]
    public string ComplaintDetails { get; set; } = null!;
}
