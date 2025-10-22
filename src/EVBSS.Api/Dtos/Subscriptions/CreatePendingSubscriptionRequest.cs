using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Subscriptions;

/// <summary>
/// Request để tạo subscription pending (chờ thanh toán)
/// Sử dụng cho flow: User chọn gói → Tạo subscription (IsActive=false) → Thanh toán → Kích hoạt
/// </summary>
public class CreatePendingSubscriptionRequest
{
    [Required(ErrorMessage = "Subscription Plan ID là bắt buộc")]
    public Guid SubscriptionPlanId { get; set; }
    
    [Required(ErrorMessage = "Vehicle ID là bắt buộc")]
    public Guid VehicleId { get; set; }
}
