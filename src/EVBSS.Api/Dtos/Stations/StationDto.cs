namespace EVBSS.Api.Dtos.Stations;

public record StationDto(
    Guid Id, 
    string Name, 
    string Address, 
    string City, 
    double Lat, 
    double Lng, 
    bool IsActive,
    TimeSpan OpenTime,
    TimeSpan CloseTime,
    string? PhoneNumber,
    string? PrimaryImageUrl,
    bool IsOpenNow
);
