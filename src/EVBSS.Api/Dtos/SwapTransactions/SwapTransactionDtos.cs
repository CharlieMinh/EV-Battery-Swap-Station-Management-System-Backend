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
    // Removed: odo tracking (legacy km-based pricing)
    
    // Thông tin pin
    public string IssuedBatterySerial { get; set; } = null!;   // Serial pin được cấp
    public string? ReturnedBatterySerial { get; set; }         // Serial pin được trả lại
    public int? BatteryHealthIssued { get; set; }              // Sức khỏe pin cấp (%)
    public int? BatteryHealthReturned { get; set; }            // Sức khỏe pin trả (%)
    
    // Thông tin thanh toán
    public string PaymentType { get; set; } = null!;           // Loại thanh toán (Thuê bao/Trả theo lần)
    public decimal SwapFee { get; set; }                       // Phí đổi pin
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
    // (km-based statistics removed)
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

/// <summary>
/// Request để lọc danh sách giao dịch đổi pin cho Admin/Staff
/// </summary>
public class AdminSwapTransactionFilterRequest
{
    public Guid? StationId { get; set; }                       // Lọc theo trạm
    public string? Status { get; set; }                        // Lọc theo trạng thái
    public DateTime? FromDate { get; set; }                    // Từ ngày
    public DateTime? ToDate { get; set; }                      // Đến ngày
    public string? SearchText { get; set; }                    // Tìm kiếm theo mã giao dịch, email, biển số xe
    public int Page { get; set; } = 1;                        // Số trang
    public int PageSize { get; set; } = 10;                   // Số item mỗi trang
}

/// <summary>
/// Response chi tiết giao dịch đổi pin cho Admin/Staff (có thêm thông tin staff)
/// </summary>
public class AdminSwapTransactionResponse : SwapTransactionResponse
{
    public Guid UserId { get; set; }                           // ID người dùng
    public string? CheckedInByStaffName { get; set; }          // Tên staff check-in
    public string? CompletedByStaffName { get; set; }          // Tên staff hoàn thành
    public Guid StationId { get; set; }                        // ID trạm
    public Guid VehicleId { get; set; }                        // ID xe
    public Guid? PaymentId { get; set; }                       // ID thanh toán
    public string? PaymentStatus { get; set; }                 // Trạng thái thanh toán
    public DateTime? CancelledAt { get; set; }                 // Thời gian hủy
    public string? CancellationReason { get; set; }            // Lý do hủy
}

/// <summary>
/// Response danh sách giao dịch đổi pin cho Admin/Staff với phân trang
/// </summary>
public class AdminSwapHistoryResponse
{
    public List<AdminSwapTransactionResponse> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}