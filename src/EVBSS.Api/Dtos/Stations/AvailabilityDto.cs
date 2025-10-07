namespace EVBSS.Api.Dtos.Stations;

/// <summary>
/// Tổng quan availability (dùng cho summary)
/// </summary>
public record AvailabilitySummaryDto(
    int Full, 
    int Charging, 
    int Maintenance, 
    int Total, 
    int Available
);

/// <summary>
/// Chi tiết availability theo từng BatteryModel
/// </summary>
public record AvailabilityByModelDto(
    Guid BatteryModelId,
    string BatteryModelName,
    int Total,
    int Full,
    int Available,
    int Charging,
    int Maintenance
);

/// <summary>
/// Response đầy đủ với summary + byBatteryModel
/// </summary>
public record StationAvailabilityDto(
    AvailabilitySummaryDto Summary,
    IReadOnlyList<AvailabilityByModelDto> ByBatteryModel
);
