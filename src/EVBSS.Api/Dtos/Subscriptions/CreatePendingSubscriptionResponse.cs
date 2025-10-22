namespace EVBSS.Api.Dtos.Subscriptions;

/// <summary>
/// Response trả về khi tạo subscription pending thành công
/// Bao gồm thông tin payment và URL thanh toán VNPay
/// </summary>
public class CreatePendingSubscriptionResponse
{
    /// <summary>
    /// ID của payment được tạo
    /// </summary>
    public Guid PaymentId { get; set; }
    
    /// <summary>
    /// ID của user subscription được tạo (chưa kích hoạt)
    /// </summary>
    public Guid UserSubscriptionId { get; set; }
    
    /// <summary>
    /// URL thanh toán VNPay (redirect user đến đây)
    /// </summary>
    public string PaymentUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Số tiền cần thanh toán
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Tên gói subscription
    /// </summary>
    public string PlanName { get; set; } = string.Empty;
    
    /// <summary>
    /// Mô tả gói subscription
    /// </summary>
    public string? PlanDescription { get; set; }
    
    /// <summary>
    /// Số lần đổi pin tối đa/tháng
    /// </summary>
    public int MaxSwapsPerMonth { get; set; }
    
    /// <summary>
    /// Thông báo thành công
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
