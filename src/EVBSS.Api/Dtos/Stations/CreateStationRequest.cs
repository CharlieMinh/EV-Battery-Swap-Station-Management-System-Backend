using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Stations;

public class CreateStationRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    [Required, StringLength(300)]
    public string Address { get; set; } = default!;

    [Required, StringLength(100)]
    public string City { get; set; } = default!;

    [Range(-90, 90)]
    public double Lat { get; set; }

    [Range(-180, 180)]
    public double Lng { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO cho việc cập nhật trạm (Admin only)
/// Tất cả các field đều optional - chỉ update field nào được cung cấp
/// </summary>
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
