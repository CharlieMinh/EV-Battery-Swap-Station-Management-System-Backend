namespace EVBSS.Api.Dtos.Stations;

public record BatteryUnitDto(
    Guid Id,
    string Serial,
    Guid BatteryModelId,
    string BatteryModelName,
    string Status,
    DateTime UpdatedAt
);
