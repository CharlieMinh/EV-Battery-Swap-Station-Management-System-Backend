using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Reservations;

public class CreateReservationRequest
{
    [Required] public Guid StationId { get; set; }
    // VehicleId replaces BatteryModelId; server resolves BatteryModel from the Vehicle
    [Required] public Guid VehicleId { get; set; }
    [Required] public DateOnly SlotDate { get; set; }
    [Required] public TimeSpan SlotStartTime { get; set; }
    [Required] public TimeSpan SlotEndTime { get; set; }
}
