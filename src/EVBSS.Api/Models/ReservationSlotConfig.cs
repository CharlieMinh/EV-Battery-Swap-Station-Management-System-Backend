namespace EVBSS.Api.Models;

/// <summary>
/// Cấu hình hệ thống slot cho reservation
/// </summary>
public static class ReservationSlotConfig
{
    /// <summary>
    /// Thời lượng mỗi slot (30 phút)
    /// </summary>
    public static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Buffer time cho check-in (±15 phút)
    /// VD: Slot 9:00-9:30 → Có thể check-in từ 8:45 đến 9:45
    /// </summary>
    public static readonly TimeSpan CheckInBuffer = TimeSpan.FromMinutes(15);
    
    /// <summary>
    /// Giờ mở cửa trạm (8:00 AM)
    /// </summary>
    public static readonly TimeSpan StationOpenTime = new TimeSpan(8, 0, 0);
    
    /// <summary>
    /// Giờ đóng cửa trạm (6:00 PM)
    /// </summary>
    public static readonly TimeSpan StationCloseTime = new TimeSpan(18, 0, 0);
    
    /// <summary>
    /// Số lượng slot mỗi ngày: (18:00 - 8:00) / 30min = 20 slots
    /// </summary>
    public static int TotalSlotsPerDay => 
        (int)((StationCloseTime - StationOpenTime).TotalMinutes / SlotDuration.TotalMinutes);
    
    /// <summary>
    /// Capacity mặc định mỗi slot (số xe tối đa có thể phục vụ cùng lúc)
    /// Có thể override per station nếu cần
    /// </summary>
    public const int DefaultSlotCapacity = 5;
    
    /// <summary>
    /// Số ngày tối đa có thể đặt trước
    /// </summary>
    public const int MaxAdvanceBookingDays = 7;
    
    /// <summary>
    /// Lấy tất cả các slot trong một ngày
    /// </summary>
    public static List<(TimeSpan Start, TimeSpan End)> GetAllSlotsForDay()
    {
        var slots = new List<(TimeSpan, TimeSpan)>();
        var current = StationOpenTime;
        
        while (current < StationCloseTime)
        {
            var next = current.Add(SlotDuration);
            slots.Add((current, next));
            current = next;
        }
        
        return slots;
    }
    
    /// <summary>
    /// Kiểm tra xem thời điểm hiện tại có trong check-in window của slot không
    /// </summary>
    public static bool IsWithinCheckInWindow(DateTime slotDate, TimeSpan slotStartTime, TimeSpan slotEndTime, DateTime now)
    {
        var slotDateTime = slotDate.Date.Add(slotStartTime);
        var windowStart = slotDateTime.Add(-CheckInBuffer);
        var windowEnd = slotDate.Date.Add(slotEndTime).Add(CheckInBuffer);
        
        return now >= windowStart && now <= windowEnd;
    }
    
    /// <summary>
    /// Lấy check-in window cho một slot
    /// </summary>
    public static (DateTime EarliestCheckIn, DateTime LatestCheckIn) GetCheckInWindow(DateTime slotDate, TimeSpan slotStartTime, TimeSpan slotEndTime)
    {
        var slotStart = slotDate.Date.Add(slotStartTime);
        var slotEnd = slotDate.Date.Add(slotEndTime);
        
        return (
            slotStart.Add(-CheckInBuffer),
            slotEnd.Add(CheckInBuffer)
        );
    }
}
