using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.BatteryUnits;

/// <summary>
/// DTO cho Staff tạo yêu cầu tăng pin
/// </summary>
public class RequestBatteryStockDto
{
    [Required(ErrorMessage = "Mã trạm là bắt buộc.")]
    public Guid StationId { get; set; }

    [Required(ErrorMessage = "Mã mô hình pin là bắt buộc.")]
    public Guid BatteryModelId { get; set; }

    [Required(ErrorMessage = "Số lượng là bắt buộc.")]
    [Range(1, 100, ErrorMessage = "Số lượng phải nằm trong khoảng 1 đến 100.")]
    public int Quantity { get; set; }

    [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? StaffNote { get; set; }
}
