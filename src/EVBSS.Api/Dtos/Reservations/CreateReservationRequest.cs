using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Reservations;

public class CreateReservationRequest
{
    [Required] public Guid StationId { get; set; }
    [Required] public Guid BatteryModelId { get; set; }
    [Required] public DateTime StartTime { get; set; } // UTC ISO-8601
}
