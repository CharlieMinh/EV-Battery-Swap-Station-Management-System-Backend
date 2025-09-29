namespace EVBSS.Api.Dtos.SwapTransactions;

/// <summary>
/// Request để bắt đầu giao dịch đổi pin từ đặt chỗ
/// </summary>
public class StartSwapRequest
{
    public Guid ReservationId { get; set; }           // ID đặt chỗ pin
    public Guid VehicleId { get; set; }               // ID xe muốn đổi pin
    public int VehicleOdometer { get; set; }          // Số km xe hiện tại
    public string? Notes { get; set; }                // Ghi chú thêm từ khách hàng
}

/// <summary>
/// Request để hoàn thành giao dịch đổi pin
/// </summary>
public class CompleteSwapRequest
{
    public string ReturnedBatterySerial { get; set; } = null!;  // Serial pin khách hàng trả lại  
    public int? BatteryHealthReturned { get; set; }             // % sức khỏe pin trả lại (0-100)
    public string? Notes { get; set; }                          // Ghi chú khi hoàn thành giao dịch
}

/// <summary>
/// Phản hồi chi tiết giao dịch đổi pin
/// </summary>
public class SwapTransactionResponse
{
    public Guid Id { get; set; }                              // ID giao dịch
    public string TransactionNumber { get; set; } = null!;    // Mã giao dịch (EVB-SWT-YYYYMMDD####)
    public string Status { get; set; } = null!;               // Trạng thái giao dịch
    
    // Thông tin người dùng & trạm
    public string UserEmail { get; set; } = null!;            // Email khách hàng
    public string StationName { get; set; } = null!;          // Tên trạm đổi pin
    public string StationAddress { get; set; } = null!;       // Địa chỉ trạm
    
    // Thông tin xe
    public string VehicleLicensePlate { get; set; } = null!;   // Biển số xe
    public string VehicleModel { get; set; } = null!;          // Model/VIN xe
    public int VehicleOdoAtSwap { get; set; }                  // Số km xe khi đổi pin
    
    // Thông tin pin
    public string IssuedBatterySerial { get; set; } = null!;   // Serial pin được cấp
    public string? ReturnedBatterySerial { get; set; }         // Serial pin được trả lại
    public int? BatteryHealthIssued { get; set; }              // Sức khỏe pin cấp (%)
    public int? BatteryHealthReturned { get; set; }            // Sức khỏe pin trả (%)
    
    // Thông tin thanh toán
    public string PaymentType { get; set; } = null!;           // Loại thanh toán (Thuê bao/Trả theo lần)
    public decimal SwapFee { get; set; }                       // Phí đổi pin
    public decimal KmChargeAmount { get; set; }                // Phí tính theo km
    public decimal TotalAmount { get; set; }                   // Tổng phí
    public bool IsPaid { get; set; }                           // Đã thanh toán chưa
    
    // Các mốc thời gian
    public DateTime StartedAt { get; set; }                    // Thời gian bắt đầu giao dịch
    public DateTime? CheckedInAt { get; set; }                 // Thời gian check-in
    public DateTime? BatteryIssuedAt { get; set; }             // Thời gian cấp pin
    public DateTime? BatteryReturnedAt { get; set; }           // Thời gian trả pin cũ
    public DateTime? CompletedAt { get; set; }                 // Thời gian hoàn thành
    
    // Thông tin bổ sung
    public string? Notes { get; set; }                         // Ghi chú
    public Guid? ReservationId { get; set; }                   // ID đặt chỗ liên quan
    public Guid? UserSubscriptionId { get; set; }              // ID gói thuê bao đang dùng
}

/// <summary>
/// Phản hồi lịch sử giao dịch đổi pin với phân trang
/// </summary>
public class SwapHistoryResponse
{
    public List<SwapTransactionResponse> Transactions { get; set; } = new();  // Danh sách giao dịch
    public int TotalCount { get; set; }                        // Tổng số giao dịch
    public int Page { get; set; }                             // Trang hiện tại
    public int PageSize { get; set; }                         // Số item mỗi trang
    public int TotalPages { get; set; }                       // Tổng số trang
}