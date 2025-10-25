namespace EVBSS.Api.Models
{
    public enum RequestStatus
    {
        PendingConfirmation, // Chờ Staff xác nhận
        Confirmed,           // Đã xác nhận và hoàn tất
        Rejected             // Bị từ chối
    }
}