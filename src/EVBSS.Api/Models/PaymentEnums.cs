namespace EVBSS.Api.Models;

public enum PaymentType
{
    Subscription = 0,        // Thuê pin theo gói
    PayPerSwap = 1,         // Trả tiền theo lần đổi

}

public enum PaymentMethod
{
    VNPay = 0,
    Cash = 1,
}

public enum PaymentStatus
{
    Pending = 0,            // Chờ thanh toán
    Processing = 1,         // Đang xử lý
    Completed = 2,          // Thành công
    Failed = 3,             // Thất bại
    Cancelled = 4,          // Đã hủy
    Refunded = 5,           // Đã hoàn tiền
    PartiallyPaid = 6       // Thanh toán một phần
}