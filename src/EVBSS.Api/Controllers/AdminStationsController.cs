using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Common;
using EVBSS.Api.Dtos.Stations;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/stations")]
[Authorize(Roles = "Admin")]
public class AdminStationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminStationsController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStationRequest req)
    {
        var st = new Station {
            Name = req.Name.Trim(),
            Address = req.Address.Trim(),
            City = req.City.Trim(),
            Lat = req.Lat,
            Lng = req.Lng,
            IsActive = req.IsActive
        };
        _db.Stations.Add(st);
        await _db.SaveChangesAsync();
        return Created($"/api/v1/stations/{st.Id}", new { st.Id });
    }

    // GET /api/v1/admin/stations?page=1&pageSize=20&includeInactive=true
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminStationDto>>> GetAll(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20, 
        [FromQuery] bool includeInactive = true)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Stations.AsNoTracking();
        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        var total = await q.CountAsync();

        var stations = await q.OrderByDescending(s => s.CreatedAt)
                             .Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToListAsync();

        var items = new List<AdminStationDto>();
        foreach (var station in stations)
        {
            var batteryStats = await _db.BatteryUnits
                .Where(b => b.StationId == station.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Full = g.Count(b => b.Status == BatteryStatus.Full),
                    Charging = g.Count(b => b.Status == BatteryStatus.Charging),
                    Maintenance = g.Count(b => b.Status == BatteryStatus.Maintenance)
                })
                .FirstOrDefaultAsync();

            items.Add(new AdminStationDto(
                station.Id,
                station.Name,
                station.Address,
                station.City,
                station.Lat,
                station.Lng,
                station.IsActive,
                station.CreatedAt,
                batteryStats?.Total ?? 0,
                batteryStats?.Full ?? 0,
                batteryStats?.Charging ?? 0,
                batteryStats?.Maintenance ?? 0
            ));
        }

        return new PagedResult<AdminStationDto> 
        { 
            Items = items, 
            Page = page, 
            PageSize = pageSize, 
            Total = total 
        };
    }

    // PUT /api/v1/admin/stations/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStationRequest req)
    {
        var station = await _db.Stations.FirstOrDefaultAsync(s => s.Id == id);
        if (station is null)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        // Update only provided fields
        if (!string.IsNullOrWhiteSpace(req.Name))
            station.Name = req.Name.Trim();
        
        if (!string.IsNullOrWhiteSpace(req.Address))
            station.Address = req.Address.Trim();
        
        if (!string.IsNullOrWhiteSpace(req.City))
            station.City = req.City.Trim();
        
        if (req.Lat.HasValue)
            station.Lat = req.Lat.Value;
        
        if (req.Lng.HasValue)
            station.Lng = req.Lng.Value;
        
        if (req.IsActive.HasValue)
            station.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync();
        
        return Ok(new { message = "Station updated successfully", stationId = station.Id });
    }

    // GET /api/v1/admin/stations/health
    [HttpGet("health")]
    public async Task<ActionResult<IReadOnlyList<StationHealthDto>>> GetStationsHealth()
    {
        var stations = await _db.Stations.AsNoTracking().ToListAsync();
        var healthData = new List<StationHealthDto>();

        foreach (var station in stations)
        {
            var batteryStats = await _db.BatteryUnits
                .Where(b => b.StationId == station.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Available = g.Count(b => b.Status == BatteryStatus.Full && !b.IsReserved),
                    Charging = g.Count(b => b.Status == BatteryStatus.Charging),
                    Maintenance = g.Count(b => b.Status == BatteryStatus.Maintenance)
                })
                .FirstOrDefaultAsync();

            var total = batteryStats?.Total ?? 0;
            var maintenance = batteryStats?.Maintenance ?? 0;
            var healthPercentage = total > 0 ? Math.Round((double)(total - maintenance) / total * 100, 1) : 100.0;

            healthData.Add(new StationHealthDto(
                station.Id,
                station.Name,
                station.IsActive,
                healthPercentage,
                total,
                batteryStats?.Available ?? 0,
                batteryStats?.Charging ?? 0,
                maintenance,
                DateTime.UtcNow
            ));
        }

        return healthData.OrderByDescending(h => h.HealthPercentage).ToList();
    }

    // PUT /api/v1/admin/stations/{id}/deactivate
    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateStation(Guid id)
    {
        var station = await _db.Stations.FirstOrDefaultAsync(s => s.Id == id);
        if (station is null)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        station.IsActive = false;
        await _db.SaveChangesAsync();
        
        return Ok(new { message = "Station deactivated successfully", stationId = station.Id });
    }

    // PUT /api/v1/admin/stations/{id}/activate
    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> ActivateStation(Guid id)
    {
        var station = await _db.Stations.FirstOrDefaultAsync(s => s.Id == id);
        if (station is null)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        station.IsActive = true;
        await _db.SaveChangesAsync();
        
        return Ok(new { message = "Station activated successfully", stationId = station.Id });
    }
}
