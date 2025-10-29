// This DTO was part of the legacy reservation-based re-swap flow which has been
// removed in favor of the single initial inspection scheduling flow.
// Keep a stub here marked Obsolete to avoid breaking builds for any remaining
// references while client code migrates. Remove this file entirely in a later
// version once callers have been updated.
using System;
using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Complaints
{
    [Obsolete("CreateReswapReservationRequest is deprecated. Use CreateInspectionReservationRequest and the inspection scheduling flow instead.")]
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
