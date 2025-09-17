using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Common;
using EVBSS.Api.Dtos.Stations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EVBSS.Api.Models;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StationsController(AppDbContext db) => _db = db;

    // GET /api/v1/stations?city=HCM&page=1&pageSize=20
    [HttpGet]
    public async Task<ActionResult<PagedResult<StationDto>>> Get([FromQuery] string? city, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Stations.AsNoTracking().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(city))
            q = q.Where(s => s.City == city);

        var total = await q.CountAsync();

        var items = await q.OrderBy(s => s.Name)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .Select(s => new StationDto(s.Id, s.Name, s.Address, s.City, s.Lat, s.Lng, s.IsActive))
                           .ToListAsync();

        return new PagedResult<StationDto> { Items = items, Page = page, PageSize = pageSize, Total = total };
    }

    // GET /api/v1/stations/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StationDto>> GetById(Guid id)
    {
        var s = await _db.Stations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (s is null) return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });
        return new StationDto(s.Id, s.Name, s.Address, s.City, s.Lat, s.Lng, s.IsActive);
    }

    // GET /api/v1/stations/nearby?lat=10.77&lng=106.7&radiusKm=5
    [HttpGet("nearby")]
    public async Task<ActionResult<IReadOnlyList<NearbyStationDto>>> Nearby([FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm = 5)
    {
        if (radiusKm <= 0 || radiusKm > 50) radiusKm = 5; // hạn chế trong dev

        // MVP: tính khoảng cách ở memory (danh sách trạm không quá lớn)
        var all = await _db.Stations.AsNoTracking().Where(s => s.IsActive).ToListAsync();
        var res = all.Select(s =>
                new NearbyStationDto(s.Id, s.Name, s.Address, s.City, s.Lat, s.Lng, HaversineKm(lat, lng, s.Lat, s.Lng)))
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .ToList();

        return res;
    }

    // Haversine đơn giản (km)
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // bán kính Trái Đất (km)
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 3);
    }
    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    // GET /api/v1/stations/{id}/availability
    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<AvailabilityDto>> Availability(Guid id)
    {
        var exists = await _db.Stations.AnyAsync(s => s.Id == id && s.IsActive);
        if (!exists)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        var agg = await _db.BatteryUnits
            .Where(b => b.StationId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Full = g.Count(b => b.Status == BatteryStatus.Full),
                Charging = g.Count(b => b.Status == BatteryStatus.Charging),
                Maintenance = g.Count(b => b.Status == BatteryStatus.Maintenance),
                Total = g.Count()
            })
            .FirstOrDefaultAsync();

        var a = agg is null ? new AvailabilityDto(0, 0, 0, 0)
                            : new AvailabilityDto(agg.Full, agg.Charging, agg.Maintenance, agg.Total);
        return a;
    }

    // GET /api/v1/stations/{id}/batteries?status=Full|Charging|Maintenance|Issued
    [HttpGet("{id:guid}/batteries")]
    public async Task<ActionResult<IReadOnlyList<BatteryUnitDto>>> Batteries(Guid id, [FromQuery] BatteryStatus? status)
    {
        var exists = await _db.Stations.AnyAsync(s => s.Id == id && s.IsActive);
        if (!exists)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        var q = _db.BatteryUnits.AsNoTracking()
            .Where(b => b.StationId == id);

        if (status.HasValue)
            q = q.Where(b => b.Status == status.Value);

        var list = await q
            .OrderByDescending(b => b.Status == BatteryStatus.Full)  // Full lên trước
            .ThenBy(b => b.Serial)
            .Select(b => new BatteryUnitDto(
                b.Id, b.Serial, b.BatteryModelId, b.Model.Name, b.Status.ToString(), b.UpdatedAt))
            .ToListAsync();

        return list;
    }
}
