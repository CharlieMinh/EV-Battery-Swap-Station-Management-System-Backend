namespace EVBSS.Api.Dtos.Subscriptions;

public class SubscriptionCreatedResponse
{
    public Guid SubscriptionId { get; set; }
    public string Message { get; set; } = null!;
    public bool RequiresDeposit { get; set; }
    public decimal DepositAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime BillingPeriodStart { get; set; }
    public DateTime BillingPeriodEnd { get; set; }
}

public class CancelSubscriptionRequest
{
    public string? Reason { get; set; }
    public DateTime? CancellationDate { get; set; } // Nếu null thì hủy ngay
}

public class CancelSubscriptionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public DateTime? EndDate { get; set; }
    public decimal? DepositRefund { get; set; }
}