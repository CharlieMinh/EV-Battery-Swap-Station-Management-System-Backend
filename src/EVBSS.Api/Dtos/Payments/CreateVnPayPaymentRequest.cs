using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Payments;

public class CreateVnPayPaymentRequest
{
    // ✅ REFACTORED: Payment for subscription directly (no invoice)
    [Required]
    public Guid SubscriptionId { get; set; }
    
    public string? OrderInfo { get; set; }  // Mô tả đơn hàng
    
    public string? IpAddress { get; set; }  // IP của client (optional)
}