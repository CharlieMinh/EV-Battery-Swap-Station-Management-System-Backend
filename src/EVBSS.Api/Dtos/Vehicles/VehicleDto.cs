namespace EVBSS.Api.Dtos.Vehicles;

public record VehicleDto(
    Guid Id,
    string Vin,
    string Plate,
    Guid CompatibleBatteryModelId,
    string CompatibleBatteryModelName,
    DateTime CreatedAt
);
