namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Request để Staff xác nhận đã nhận tiền mặt
/// </summary>
public class ConfirmCashPaymentRequest
{
    /// <summary>
    /// Ghi chú của Staff (optional)
    /// </summary>
    public string? Notes { get; set; }
}
