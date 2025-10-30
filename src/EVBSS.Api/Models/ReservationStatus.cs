namespace EVBSS.Api.Models;
public enum ReservationStatus
{
    Pending = 0,      // Đã đặt lịch, chờ đến slot
    CheckedIn = 1,    // Đã quét QR tại trạm 
    Completed = 2,    // Đã hoàn thành đổi pin
    Cancelled = 3,    // Đã hủy
    Expired = 4       // Hết hạn (không check-in trong slot window)
}
