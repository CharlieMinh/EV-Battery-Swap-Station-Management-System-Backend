namespace EVBSS.Api.Dtos.Reservations;

public record ReservationDto(
    Guid Id,
    Guid StationId,
    Guid BatteryModelId,
    Guid BatteryUnitId,
    string Status,
    DateTime StartTime,
    int HoldDurationMinutes,
    DateTime CreatedAt
);
