using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Vehicles;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // yêu cầu JWT (Driver)
public class VehiclesController : ControllerBase
{
    private readonly AppDbContext _db;
    public VehiclesController(AppDbContext db) => _db = db;

    // Lấy userId từ token (sub hoặc NameIdentifier)
    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out userId);
    }

    /// GET /api/v1/vehicles (xe của tôi)
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var items = await _db.Vehicles.AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VehicleDto(
                v.Id, v.VIN, v.Plate,
                v.CompatibleBatteryModelId, v.CompatibleModel.Name, v.CreatedAt))
            .ToListAsync();

        return Ok(items);
    }

    /// GET /api/v1/vehicles/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var v = await _db.Vehicles.AsNoTracking()
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new VehicleDto(
                x.Id, x.VIN, x.Plate,
                x.CompatibleBatteryModelId, x.CompatibleModel.Name, x.CreatedAt))
            .FirstOrDefaultAsync();

        return v is null
            ? NotFound(new { error = new { code = "VEHICLE_NOT_FOUND", message = "Vehicle not found" } })
            : Ok(v);
    }

    /// POST /api/v1/vehicles
    [HttpPost]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest req)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var vin = req.Vin.Trim().ToUpperInvariant();
        var plate = req.Plate.Trim().ToUpperInvariant();

        // Model pin tồn tại?
        var bm = await _db.BatteryModels.FindAsync(req.CompatibleBatteryModelId);
        if (bm is null)
            return BadRequest(new { error = new { code = "BATTERY_MODEL_NOT_FOUND", message = "Compatible battery model not found." } });

        // Không trùng trong phạm vi user
        if (await _db.Vehicles.AnyAsync(v => v.UserId == userId && v.VIN == vin))
            return Conflict(new { error = new { code = "VIN_EXISTS", message = "VIN already exists." } });

        if (await _db.Vehicles.AnyAsync(v => v.UserId == userId && v.Plate == plate))
            return Conflict(new { error = new { code = "PLATE_EXISTS", message = "Plate already exists." } });

        var entity = new Vehicle
        {
            UserId = userId,
            VIN = vin,
            Plate = plate,
            CompatibleBatteryModelId = bm.Id
        };

        _db.Vehicles.Add(entity);
        await _db.SaveChangesAsync();

        var dto = new VehicleDto(entity.Id, entity.VIN, entity.Plate,
                                 entity.CompatibleBatteryModelId, bm.Name, entity.CreatedAt);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    /// DELETE /api/v1/vehicles/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (v is null)
            return NotFound(new { error = new { code = "VEHICLE_NOT_FOUND", message = "Vehicle not found" } });

        _db.Vehicles.Remove(v);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
