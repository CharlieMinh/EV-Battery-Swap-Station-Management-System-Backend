using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Subscriptions;

public class CreateSubscriptionRequest
{
    [Required]
    public Guid SubscriptionPlanId { get; set; }
    
    [Required]
    public Guid VehicleId { get; set; }
    
    public DateTime? StartDate { get; set; } // Nếu null thì bắt đầu ngay
    
    public string? Notes { get; set; }
}