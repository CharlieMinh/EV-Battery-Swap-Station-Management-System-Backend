namespace EVBSS.Api.Dtos.Invoices;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string Type { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    
    // Billing period for subscription invoices
    public DateTime? BillingPeriodStart { get; set; }
    public DateTime? BillingPeriodEnd { get; set; }
    public int? KmUsedInPeriod { get; set; }
    
    // Financial details
    public decimal SubtotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal OverdueFeeAmount { get; set; }
    
    // Status
    public string Status { get; set; } = null!;
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public string? Notes { get; set; }
    
    // Subscription info (if applicable)
    public Guid? UserSubscriptionId { get; set; }
    public string? SubscriptionPlanName { get; set; }
    public string? VehiclePlate { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}