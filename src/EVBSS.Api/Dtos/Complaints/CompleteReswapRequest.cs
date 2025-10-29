using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Complaints;

/// <summary>
/// Yêu cầu để Staff hoàn tất giao dịch đổi pin miễn phí (Re-swap), bao gồm thông tin về pin lỗi đã thu hồi.
/// </summary>
    public class CompleteReswapRequest
{
    [Range(0, 100, ErrorMessage = "Tình trạng pin phải nằm trong khoảng từ 0 đến 100.")]
    public int? ReturnedBatteryHealth { get; set; }
}
