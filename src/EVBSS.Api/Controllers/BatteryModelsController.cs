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
            .Select(m => new { m.Id, m.Name, m.Voltage, m.CapacityWh })
            .ToListAsync());
}
