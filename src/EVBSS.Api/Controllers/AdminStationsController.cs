using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Stations;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
