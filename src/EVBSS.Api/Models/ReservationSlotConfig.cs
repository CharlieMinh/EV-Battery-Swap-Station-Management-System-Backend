namespace EVBSS.Api.Models;

/// <summary>
/// Cấu hình hệ thống slot cho reservation
/// </summary>
public static class ReservationSlotConfig
{
    public static readonly TimeSpan SlotDuration = TimeSpan.FromMinutes(30);    /// Thời lượng mỗi slot (30 phút)
    public static readonly TimeSpan CheckInBuffer = TimeSpan.FromMinutes(15);    /// Buffer time cho check-in (±15 phút)
    public static readonly TimeSpan StationOpenTime = new TimeSpan(8, 0, 0);    /// Giờ mở cửa trạm (8:00 AM)
    public static readonly TimeSpan StationCloseTime = new TimeSpan(18, 0, 0);
    
    /// <summary>
    /// Số lượng slot mỗi ngày: (18:00 - 8:00) / 30min = 20 slots
    /// </summary>
    public static int TotalSlotsPerDay => 
        (int)((StationCloseTime - StationOpenTime).TotalMinutes / SlotDuration.TotalMinutes);
    public const int DefaultSlotCapacity = 5;    /// Capacity mặc định mỗi slot (số xe tối đa có thể phục vụ cùng lúc)
    public const int MaxAdvanceBookingDays = 14;    /// Số ngày tối đa có thể đặt trước 
    public static List<(TimeSpan Start, TimeSpan End)> GetAllSlotsForDay()   /// Lấy tất cả các slot trong một ngày
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
    /// UPDATED: Always return true - Allow check-in anytime for demo/testing
    /// </summary>
    public static bool IsWithinCheckInWindow(DateTime slotDate, TimeSpan slotStartTime, TimeSpan slotEndTime, DateTime now)
    {
        // ⭐ ALWAYS ALLOW CHECK-IN - Removed time window validation
        return true;
        
        // Original logic (commented out for future reference):
        // var slotDateTime = slotDate.Date.Add(slotStartTime);
        // var windowStart = slotDateTime.Add(-CheckInBuffer);
        // var windowEnd = slotDate.Date.Add(slotEndTime).Add(CheckInBuffer);
        // return now >= windowStart && now <= windowEnd;
    }
    
    /// <summary>
    /// Kiểm tra xem thời điểm hiện tại có trong check-in window của slot không (DateOnly overload)
    /// UPDATED: Always return true - Allow check-in anytime for demo/testing
    /// </summary>
    public static bool IsWithinCheckInWindow(DateOnly slotDate, TimeSpan slotStartTime, TimeSpan slotEndTime, DateTime now)
    {
        // ⭐ ALWAYS ALLOW CHECK-IN - Removed time window validation
        return true;
        
        // Original logic (commented out for future reference):
        // var slotDateTime = slotDate.ToDateTime(TimeOnly.FromTimeSpan(slotStartTime));
        // var windowStart = slotDateTime.Add(-CheckInBuffer);
        // var windowEnd = slotDate.ToDateTime(TimeOnly.FromTimeSpan(slotEndTime)).Add(CheckInBuffer);
        // return now >= windowStart && now <= windowEnd;
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
    
    /// <summary>
    /// Lấy check-in window cho một slot (DateOnly overload)
    /// </summary>
    public static (DateTime EarliestCheckIn, DateTime LatestCheckIn) GetCheckInWindow(DateOnly slotDate, TimeSpan slotStartTime, TimeSpan slotEndTime)
    {
        var slotStart = slotDate.ToDateTime(TimeOnly.FromTimeSpan(slotStartTime));
        var slotEnd = slotDate.ToDateTime(TimeOnly.FromTimeSpan(slotEndTime));
        
        return (
            slotStart.Add(-CheckInBuffer),
            slotEnd.Add(CheckInBuffer)
        );
    }
}
