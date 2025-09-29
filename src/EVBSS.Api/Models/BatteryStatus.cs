namespace EVBSS.Api.Models;

public enum BatteryStatus
{
    Full = 0,        // Pin đầy, sẵn sàng sử dụng
    Charging = 1,    // Pin đang sạc
    Maintenance = 2, // Pin đang bảo trì
    Issued = 3       // Pin đã được cấp cho khách hàng
}
