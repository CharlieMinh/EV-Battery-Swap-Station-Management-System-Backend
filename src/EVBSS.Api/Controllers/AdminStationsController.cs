using EVBSS.Api.Data;
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
    private readonly ILogger<AdminStationsController> _logger;

    public AdminStationsController(AppDbContext db, ILogger<AdminStationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// [Admin] Tạo trạm mới
    /// POST /api/v1/admin/stations
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStationRequest req)
    {
        // Validation tọa độ
        if (req.Lat < -90 || req.Lat > 90)
            return BadRequest(new { error = new { code = "INVALID_LATITUDE", message = "Latitude must be between -90 and 90" } });

        if (req.Lng < -180 || req.Lng > 180)
            return BadRequest(new { error = new { code = "INVALID_LONGITUDE", message = "Longitude must be between -180 and 180" } });

        var st = new Station
        {
            Name = req.Name.Trim(),
            Address = req.Address.Trim(),
            City = req.City.Trim(),
            Lat = req.Lat,
            Lng = req.Lng,
            IsActive = req.IsActive
        };

        _db.Stations.Add(st);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin created new station: {StationId} - {StationName}", st.Id, st.Name);

        return Created($"/api/v1/stations/{st.Id}", new
        {
            id = st.Id,
            name = st.Name,
            address = st.Address,
            city = st.City,
            lat = st.Lat,
            lng = st.Lng,
            isActive = st.IsActive
        });
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin trạm
    /// PUT /api/v1/admin/stations/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStationRequest req)
    {
        var station = await _db.Stations.FindAsync(id);
        if (station is null)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        // Validation tọa độ (nếu có update)
        if (req.Lat.HasValue && (req.Lat.Value < -90 || req.Lat.Value > 90))
            return BadRequest(new { error = new { code = "INVALID_LATITUDE", message = "Latitude must be between -90 and 90" } });

        if (req.Lng.HasValue && (req.Lng.Value < -180 || req.Lng.Value > 180))
            return BadRequest(new { error = new { code = "INVALID_LONGITUDE", message = "Longitude must be between -180 and 180" } });

        // Update từng field nếu có giá trị
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

        _logger.LogInformation("Admin updated station: {StationId} - {StationName}", station.Id, station.Name);

        return Ok(new
        {
            id = station.Id,
            name = station.Name,
            address = station.Address,
            city = station.City,
            lat = station.Lat,
            lng = station.Lng,
            isActive = station.IsActive,
            message = "Station updated successfully"
        });
    }

    /// <summary>
    /// [Admin] Xóa trạm
    /// DELETE /api/v1/admin/stations/{id}
    /// - Nếu trạm có pin: SOFT DELETE (set IsActive = false)
    /// - Nếu trạm không có pin: HARD DELETE (xóa khỏi database)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var station = await _db.Stations.FindAsync(id);
        if (station is null)
            return NotFound(new { error = new { code = "STATION_NOT_FOUND", message = "Station not found" } });

        // Kiểm tra xem trạm có pin không
        var hasBatteries = await _db.BatteryUnits.AnyAsync(b => b.StationId == id);

        if (hasBatteries)
        {
            // SOFT DELETE: Chỉ set IsActive = false
            station.IsActive = false;
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin soft-deleted station (has batteries): {StationId} - {StationName}", station.Id, station.Name);

            return Ok(new
            {
                message = "Station deactivated (soft delete) because it has batteries",
                stationId = station.Id,
                deletionType = "soft",
                isActive = station.IsActive
            });
        }
        else
        {
            // HARD DELETE: Xóa hoàn toàn khỏi database
            _db.Stations.Remove(station);
            await _db.SaveChangesAsync();

            _logger.LogWarning("Admin hard-deleted station (no batteries): {StationId} - {StationName}", id, station.Name);

            return Ok(new
            {
                message = "Station permanently deleted (hard delete)",
                stationId = id,
                deletionType = "hard"
            });
        }
    }
}
