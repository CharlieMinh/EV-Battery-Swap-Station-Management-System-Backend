namespace EVBSS.Api.Dtos.Vehicles;

public record VehicleDto(
    Guid Id,
    string Vin,
    string Plate,
    Guid? VehicleModelId,            // Nullable tạm thời
    string? VehicleModelName,        // VF3, VF5
    string? VehicleModelFullName,    // VinFast VF3
    string? Brand,                   // VinFast
    Guid CompatibleBatteryModelId,
    string CompatibleBatteryModelName,
    string? PhotoUrl,
    string? RegistrationPhotoUrl,    // Ảnh cà vẹt xe
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
