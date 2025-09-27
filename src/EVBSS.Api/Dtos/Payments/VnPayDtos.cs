namespace EVBSS.Api.Dtos.Payments;

public class VnPayPaymentResponse
{
    public bool Success { get; set; }
    public string? PaymentUrl { get; set; }
    public string? PaymentReference { get; set; }
    public string? Message { get; set; }
    public Guid PaymentId { get; set; }
}

public class VnPayCallbackRequest
{
    public string vnp_Amount { get; set; } = null!;
    public string vnp_BankCode { get; set; } = null!;
    public string vnp_BankTranNo { get; set; } = null!;
    public string vnp_CardType { get; set; } = null!;
    public string vnp_OrderInfo { get; set; } = null!;
    public string vnp_PayDate { get; set; } = null!;
    public string vnp_ResponseCode { get; set; } = null!;
    public string vnp_TmnCode { get; set; } = null!;
    public string vnp_TransactionNo { get; set; } = null!;
    public string vnp_TransactionStatus { get; set; } = null!;
    public string vnp_TxnRef { get; set; } = null!;
    public string vnp_SecureHash { get; set; } = null!;
}

public class VnPayCallbackResponse
{
    public string RspCode { get; set; } = "00";  // 00: Success, other: Error
    public string Message { get; set; } = "Confirm Success";
}