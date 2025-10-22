namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Response khi Staff xác nhận thanh toán tiền mặt
/// </summary>
public class ConfirmCashPaymentResponse
{
    /// <summary>
    /// Thành công hay thất bại
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Thông báo
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// ID của payment đã được confirm
    /// </summary>
    public Guid PaymentId { get; set; }
    
    /// <summary>
    /// Có kích hoạt subscription không
    /// </summary>
    public bool SubscriptionActivated { get; set; }
    
    /// <summary>
    /// ID của subscription đã được kích hoạt (nếu có)
    /// </summary>
    public Guid? SubscriptionId { get; set; }
}
