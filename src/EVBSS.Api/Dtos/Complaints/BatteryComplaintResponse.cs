using System;
using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.Complaints
{
    public class BatteryComplaintResponse
    {
        public Guid Id { get; set; }
        public Guid SwapTransactionId { get; set; }
        public Guid IssuedBatteryId { get; set; }
        public Guid ReportedByUserId { get; set; }
        public ComplaintStatus Status { get; set; }
        public string ComplaintDetails { get; set; } = null!;
        public DateTime ReportDate { get; set; }
        public Guid? HandledByStaffId { get; set; }
        public string? ResolutionNotes { get; set; }
        public DateTime? ResolvedAt { get; set; }
        // Optional: include some navigation info
        public string? IssuedBatterySerial { get; set; }
        public string? StationName { get; set; }
    }
}
