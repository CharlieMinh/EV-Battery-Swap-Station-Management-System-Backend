namespace EVBSS.Api.Dtos.Stations;

public record NearbyStationDto(Guid Id, string Name, string Address, string City, double Lat, double Lng, double DistanceKm);
