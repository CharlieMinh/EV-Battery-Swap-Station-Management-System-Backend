using System;

namespace EVBSS.Api.Models
{
    public class BulkCreateRequest
    {
        public Guid Id { get; set; }
        public Guid StationId { get; set; }
        public Station Station { get; set; }
        public Guid BatteryModelId { get; set; }
        public BatteryModel BatteryModel { get; set; }
        public int Quantity { get; set; }
        public RequestStatus Status { get; set; }

        public Guid RequestedByAdminId { get; set; } // ID của Admin yêu cầu
        public User RequestedByAdmin { get; set; }

        public Guid? HandledByStaffId { get; set; } // ID của Staff xử lý (nullable)
        public User? HandledByStaff { get; set; }
        
        public string? StaffNotes { get; set; } // Ghi chú của nhân viên khi xử lý (confirm/reject)

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}