namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Response khi regenerate VNPay payment URL
/// </summary>
public class RegenerateVnPayUrlResponse
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
    /// ID của payment
    /// </summary>
    public Guid PaymentId { get; set; }

    /// <summary>
    /// VNPay payment URL mới
    /// </summary>
    public string? PaymentUrl { get; set; }

    /// <summary>
    /// Số tiền cần thanh toán
    /// </summary>
    public decimal Amount { get; set; }
}
