namespace EVBSS.Api.Dtos.SwapTransactions;

/// <summary>
/// Request để bắt đầu giao dịch đổi pin
/// </summary>
public class StartSwapRequest
{
    public Guid StationId { get; set; }               // ID trạm đổi pin
    public Guid VehicleId { get; set; }               // ID xe muốn đổi pin
    public Guid? ReservationId { get; set; }          // ID đặt chỗ pin (optional)
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
    
    // Feedback và đánh giá
    public int? Rating { get; set; }                           // Đánh giá 1-5 sao
    public string? Feedback { get; set; }                      // Phản hồi chi tiết
    public DateTime? RatedAt { get; set; }                     // Thời gian đánh giá
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

/// <summary>
/// Request để cấp pin cho khách hàng (dành cho Staff)
/// </summary>
public class IssueBatteryRequest
{
    public Guid BatteryUnitId { get; set; }                   // ID pin cấp cho khách
    public string? Notes { get; set; }                        // Ghi chú từ staff
}

/// <summary>
/// Request để nhận pin cũ từ khách hàng (dành cho Staff)
/// </summary>
public class ReceiveBatteryRequest
{
    public string ReturnedBatterySerial { get; set; } = null!; // Serial pin khách trả lại  
    public int BatteryHealthReturned { get; set; }             // % sức khỏe pin trả lại (0-100)
    public string? Notes { get; set; }                         // Ghi chú từ staff
}

/// <summary>
/// Thống kê chi tiết lịch sử đổi pin của người dùng
/// </summary>
public class SwapStatisticsResponse
{
    // Thống kê tổng quan
    public int TotalSwaps { get; set; }                        // Tổng số lần đổi pin
    public int CompletedSwaps { get; set; }                    // Số lần đổi thành công
    public int CancelledSwaps { get; set; }                    // Số lần hủy
    public int FailedSwaps { get; set; }                       // Số lần thất bại
    public decimal SuccessRate { get; set; }                   // Tỷ lệ thành công (%)
    
    // Thống kê tài chính
    public decimal TotalAmount { get; set; }                   // Tổng chi phí
    public decimal AverageSwapFee { get; set; }                // Chi phí trung bình mỗi lần đổi
    public decimal TotalKmCharges { get; set; }                // Tổng phí theo km
    
    // Thống kê xe và pin
    public int TotalKilometers { get; set; }                   // Tổng số km đã chạy (tracking qua swap)
    public int AverageKmPerSwap { get; set; }                  // Trung bình km mỗi lần đổi
    public int AverageBatteryHealthIssued { get; set; }        // Sức khỏe pin trung bình được cấp
    public int AverageBatteryHealthReturned { get; set; }      // Sức khỏe pin trung bình được trả
    
    // Thống kê thời gian
    public DateTime? FirstSwapDate { get; set; }               // Lần đổi đầu tiên
    public DateTime? LastSwapDate { get; set; }                // Lần đổi gần nhất
    public int DaysSinceFirstSwap { get; set; }                // Số ngày từ lần đổi đầu
    public double AverageSwapsPerMonth { get; set; }           // Trung bình số lần đổi/tháng
    
    // Thống kê trạm được sử dụng nhiều nhất
    public string? MostUsedStationName { get; set; }           // Trạm được dùng nhiều nhất
    public int MostUsedStationCount { get; set; }              // Số lần sử dụng trạm đó
    
    // Feedback và đánh giá
    public double? AverageRating { get; set; }                 // Đánh giá trung bình
    public int TotalFeedbacks { get; set; }                    // Số lượng feedback đã đưa
    
    // Thống kê theo thời gian gần đây
    public int SwapsLast30Days { get; set; }                   // Số lần đổi trong 30 ngày qua
    public int SwapsLast7Days { get; set; }                    // Số lần đổi trong 7 ngày qua
}

/// <summary>
/// Response cho việc đánh giá và phản hồi giao dịch đổi pin
/// </summary>
public class SwapRatingRequest
{
    public int Rating { get; set; }                            // Đánh giá từ 1-5 sao
    public string? Feedback { get; set; }                      // Phản hồi chi tiết
    public List<string>? Issues { get; set; }                  // Các vấn đề gặp phải (nếu có)
}