namespace EVBSS.Api.Dtos.Stations;

public record StationDto(Guid Id, string Name, string Address, string City, double Lat, double Lng, bool IsActive);
