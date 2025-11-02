using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.BatteryUnits;

/// <summary>
/// DTO cho Admin duyệt hoặc từ chối yêu cầu
/// </summary>
public class ReviewBatteryStockRequestDto
{
    [Required(ErrorMessage = "Trạng thái duyệt là bắt buộc.")]
    public bool IsApproved { get; set; } // True: Duyệt, False: Từ chối

    [MaxLength(500, ErrorMessage = "Ghi chú/Lý do không được vượt quá 500 ký tự.")]
    public string? AdminNote { get; set; }
}
