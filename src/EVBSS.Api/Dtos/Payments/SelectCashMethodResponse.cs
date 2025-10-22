namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Response khi user chọn thanh toán bằng tiền mặt thay vì VNPay
/// </summary>
public class SelectCashMethodResponse
{
    /// <summary>
    /// Thành công hay thất bại
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Thông báo cho user
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// ID của payment đã được chuyển sang Cash method
    /// </summary>
    public Guid PaymentId { get; set; }
    
    /// <summary>
    /// Số tiền cần thanh toán
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Hướng dẫn cho user (đến trạm nào để thanh toán)
    /// </summary>
    public string Instructions { get; set; } = string.Empty;
}
