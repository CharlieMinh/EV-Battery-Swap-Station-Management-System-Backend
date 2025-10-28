// File: src/EVBSS.Api/Models/BatteryStatus.cs
namespace EVBSS.Api.Models;

public enum BatteryStatus
{    // Pin sẵn sàng để được gán cho khách
    Full = 0,

    // Pin đã được gán cho một yêu cầu đặt lịch (reservation) và đang chờ khách đến lấy
    Reserved = 1,

    // Pin đang trong quá trình sử dụng bởi khách hàng
    InUse = 2,

    // Pin đang được sạc tại trạm
    Charging = 3,

    // Pin cần được sạc (trạng thái của pin cũ khi khách trả lại)
    Depleted = 4,

    // Pin đang trong quá trình bảo trì, không thể sử dụng
    Maintenance = 5,
    // ⭐ THÊM MỚI: Pin được báo lỗi và đang chờ kiểm tra
    Faulty = 6 // Pin đã được Staff xác nhận là lỗi hệ thống/bảo hành
}