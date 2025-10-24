namespace EVBSS.Api.Dtos.Payments;

public class CompleteCashPaymentResponse
{
    public bool Success { get; set; }
    public Guid PaymentId { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
}
