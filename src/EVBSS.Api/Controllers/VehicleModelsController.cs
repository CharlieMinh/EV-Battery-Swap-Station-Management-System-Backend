using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Vehicles;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/vehicle-models")]
public class VehicleModelsController : ControllerBase
{
    private readonly AppDbContext _db;
    public VehicleModelsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lấy danh sách loại xe của hãng (public - không cần auth)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VehicleModelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive)
    {
        var query = _db.VehicleModels.AsNoTracking();

        if (isActive.HasValue)
            query = query.Where(vm => vm.IsActive == isActive.Value);

        var items = await query
            .Include(vm => vm.CompatibleBatteryModel)
            .OrderBy(vm => vm.Brand)
            .ThenBy(vm => vm.Name)
            .Select(vm => new VehicleModelDto(
                vm.Id,
                vm.Name,
                vm.FullName,
                vm.Brand,
                vm.CompatibleBatteryModelId,
                vm.CompatibleBatteryModel.Name,
                vm.ImageUrl,
                vm.IsActive,
                vm.Description
            ))
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>
    /// Lấy chi tiết 1 loại xe
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleModelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var vm = await _db.VehicleModels.AsNoTracking()
            .Include(vm => vm.CompatibleBatteryModel)
            .Where(vm => vm.Id == id)
            .Select(vm => new VehicleModelDto(
                vm.Id,
                vm.Name,
                vm.FullName,
                vm.Brand,
                vm.CompatibleBatteryModelId,
                vm.CompatibleBatteryModel.Name,
                vm.ImageUrl,
                vm.IsActive,
                vm.Description
            ))
            .FirstOrDefaultAsync();

        return vm is null
            ? NotFound(new { error = new { code = "VEHICLE_MODEL_NOT_FOUND", message = "Vehicle model not found" } })
            : Ok(vm);
    }

    // TODO: Thêm các endpoint cho Admin quản lý VehicleModel
    // POST /api/v1/vehicle-models (Admin only)
    // PUT /api/v1/vehicle-models/{id} (Admin only)
    // DELETE /api/v1/vehicle-models/{id} (Admin only)
}
