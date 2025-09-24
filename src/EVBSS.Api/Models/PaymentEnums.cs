namespace EVBSS.Api.Models;

public enum PaymentType
{
    Subscription = 0,        // Thuê pin theo gói
    PayPerSwap = 1,         // Trả tiền theo lần đổi
    BuyOutright = 2,        // Mua đứt pin  
    TradeIn = 3             // Thu cũ đổi mới
}

public enum PaymentMethod
{
    VNPay = 0,
    Cash = 1,
    BankTransfer = 2,
    Momo = 3
}

public enum PaymentStatus
{
    Pending = 0,            // Chờ thanh toán
    Processing = 1,         // Đang xử lý
    Completed = 2,          // Thành công
    Failed = 3,             // Thất bại
    Cancelled = 4,          // Đã hủy
    Refunded = 5            // Đã hoàn tiền
}