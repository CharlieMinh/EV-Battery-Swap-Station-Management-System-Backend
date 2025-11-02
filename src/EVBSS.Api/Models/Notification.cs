using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EVBSS.Api.Models
{
    public class Notification
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        public Guid? SenderId { get; set; }

        [ForeignKey("SenderId")]
        public User? Sender { get; set; }

        [Required]
        [MaxLength(500)]
        public string Message { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public NotificationType Type { get; set; }

        public Guid? RelatedEntityId { get; set; }
    }

    public enum NotificationType
    {
        Generic,
        NewBulkRequest,
        BulkRequestConfirmed,
        BulkRequestRejected,
        StockRequestCreated,      // Staff tạo yêu cầu tăng pin
        StockRequestApproved,     // Admin duyệt yêu cầu
        StockRequestRejected      // Admin từ chối yêu cầu
    }
}
