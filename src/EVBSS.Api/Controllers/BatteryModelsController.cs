using EVBSS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/battery-models")]
public class BatteryModelsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BatteryModelsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.BatteryModels.AsNoTracking()
            .Select(m => new { m.Id, m.Name, m.Voltage, m.CapacityWh })
            .ToListAsync());
}
