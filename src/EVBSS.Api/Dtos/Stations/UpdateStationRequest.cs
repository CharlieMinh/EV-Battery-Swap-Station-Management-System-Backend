using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Stations;

public class UpdateStationRequest
{
    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [Range(-90, 90)]
    public double? Lat { get; set; }

    [Range(-180, 180)]
    public double? Lng { get; set; }

    public bool? IsActive { get; set; }
}