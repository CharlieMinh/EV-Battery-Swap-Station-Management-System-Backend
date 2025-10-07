namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Thông tin model xe của hãng (VF3, VF5, VF8, VF9)
/// </summary>
public record VehicleModelDto(
    Guid Id,
    string Name,
    string FullName,
    string Brand,
    Guid CompatibleBatteryModelId,
    string CompatibleBatteryModelName,
    string? ImageUrl,
    bool IsActive,
    string? Description
);
