namespace EVBSS.Api.Dtos.Payments;

/// <summary>
/// Response DTO sau khi tạo reservation + payment cho pay-per-swap
/// Response format khác nhau tùy theo PaymentMethod:
/// - VNPay: Trả về PaymentUrl để redirect user
/// - Cash: Trả về QRCode và Instructions
/// </summary>
public class CreatePayPerSwapReservationResponse
{
    /// <summary>
    /// Trạng thái thành công hay thất bại
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Thông báo kết quả
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// URL thanh toán VNPay (chỉ có khi PaymentMethod = VNPay)
    /// Frontend sẽ redirect user đến URL này
    /// </summary>
    public string? PaymentUrl { get; set; }
    
    /// <summary>
    /// ID của reservation vừa tạo
    /// </summary>
    public Guid? ReservationId { get; set; }
    
    /// <summary>
    /// ID của payment vừa tạo
    /// </summary>
    public Guid? PaymentId { get; set; }
    
    /// <summary>
    /// Mã QR để check-in tại trạm (chỉ có khi PaymentMethod = Cash)
    /// User cần xuất trình mã này cho staff
    /// </summary>
    public string? QRCode { get; set; }
    
    /// <summary>
    /// Trạng thái của reservation (VD: "Pending")
    /// </summary>
    public string? Status { get; set; }
    
    /// <summary>
    /// Số tiền cần thanh toán
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Hướng dẫn cho user (chỉ có khi PaymentMethod = Cash)
    /// VD: "Vui lòng đến trạm đúng giờ và thanh toán tiền mặt 25.000 VND"
    /// </summary>
    public string? Instructions { get; set; }
}
