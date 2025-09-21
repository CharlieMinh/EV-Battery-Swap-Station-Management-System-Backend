namespace EVBSS.Api.Dtos.Stations;

public record AdminStationDto(
    Guid Id, 
    string Name, 
    string Address, 
    string City, 
    double Lat, 
    double Lng, 
    bool IsActive,
    DateTime CreatedAt,
    int TotalBatteryUnits,
    int FullBatteryUnits,
    int ChargingBatteryUnits,
    int MaintenanceBatteryUnits
);

public record StationHealthDto(
    Guid StationId,
    string StationName,
    bool IsActive,
    double HealthPercentage,
    int TotalUnits,
    int AvailableUnits,
    int ChargingUnits,
    int MaintenanceUnits,
    DateTime LastUpdated
);