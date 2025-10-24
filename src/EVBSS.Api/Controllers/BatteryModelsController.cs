using EVBSS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BatteryModelsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BatteryModelsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.BatteryModels.AsNoTracking()
            .Select(m => new { 
                m.Id, 
                m.Name, 
                m.Voltage, 
                m.CapacityWh, 
                m.SwapPricePerSession  // ⭐ Frontend cần giá này cho Pay-per-Swap
            })
            .ToListAsync());
    
    /// <summary>
    /// ⭐ API mới: Get pay-per-swap price cho 1 battery model cụ thể
    /// Frontend gọi API này khi user chọn xe → Hiển thị giá đổi pin lẻ
    /// </summary>
    [HttpGet("{id}/swap-price")]
    public async Task<IActionResult> GetSwapPrice(Guid id)
    {
        var batteryModel = await _db.BatteryModels
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (batteryModel == null)
            return NotFound(new { message = "Battery model not found" });
        
        return Ok(new { 
            batteryModelId = batteryModel.Id,
            batteryModelName = batteryModel.Name,
            capacityWh = batteryModel.CapacityWh,
            swapPricePerSession = batteryModel.SwapPricePerSession,
            currency = "VND"
        });
    }
}
