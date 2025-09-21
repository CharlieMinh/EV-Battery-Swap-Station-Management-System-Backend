namespace EVBSS.Api.Dtos.Reports;

public record DailySwapReportDto(
    DateTime Date,
    int TotalSwaps,
    int SuccessfulSwaps,
    int FailedSwaps,
    decimal TotalRevenue,
    int UniqueUsers
);

public record RevenueReportDto(
    DateTime Date,
    decimal SubscriptionRevenue,
    decimal SwapRevenue,
    decimal TotalRevenue,
    int NewSubscriptions,
    int ActiveSubscriptions
);

public record InventoryReportDto(
    Guid StationId,
    string StationName,
    string City,
    int TotalBatteries,
    int FullBatteries,
    int ChargingBatteries,
    int MaintenanceBatteries,
    double UtilizationRate,
    DateTime LastUpdated
);

public record SystemOverviewDto(
    int TotalUsers,
    int ActiveUsers,
    int TotalStations,
    int ActiveStations,
    int TotalBatteries,
    int AvailableBatteries,
    int TodaySwaps,
    decimal TodayRevenue,
    int ActiveSubscriptions,
    DateTime LastUpdated
);

public record TopStationDto(
    Guid StationId,
    string StationName,
    string City,
    int SwapCount,
    decimal Revenue,
    double UtilizationRate
);

public record SwapTrendDto(
    DateTime Date,
    int SwapCount,
    int PeakHourSwaps,
    string PeakHour
);