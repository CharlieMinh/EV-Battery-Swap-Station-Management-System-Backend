using System;
using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Complaints
{
    public class CreateReswapReservationRequest
    {
        [Required]
        public Guid ComplaintId { get; set; }

        [Required]
        public Guid StationId { get; set; }

        [Required]
        public DateTime SlotDateTime { get; set; }

        public Guid? VehicleId { get; set; }
    }
}
