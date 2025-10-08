namespace EVBSS.Api.Models;

/// <summary>
/// Lý do hủy reservation
/// </summary>
public enum CancelReason
{
    UserCancelled = 0,           // User tự hủy
    NoShow = 1,                  // Không đến trong slot (auto-cancel)
    NoBatteryAvailable = 2,      // Trạm hết pin đúng model
    StationClosed = 3,           // Trạm đóng cửa đột xuất
    SystemError = 4,             // Lỗi hệ thống
    StaffCancelled = 5           // Staff hủy (emergency)
}
