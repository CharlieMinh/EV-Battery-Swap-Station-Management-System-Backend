using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EVBSS.Api.Dtos.Reservations;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly ReservationService _service;
    public ReservationsController(ReservationService service) => _service = service;

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out userId);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReservationCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest req)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var (res, expiresAt) = await _service.HoldAsync(userId, req.StationId, req.BatteryModelId, req.StartTime);
            return CreatedAtAction(nameof(GetMine), new { }, new ReservationCreatedResponse(res.Id, res.Status.ToString(), expiresAt));
        }
        catch (NoBatteryException)
        {
            return BadRequest(new { error = new { code = "NO_BATTERY", message = "No full battery available." } });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine([FromQuery] ReservationStatus? status)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var list = await _service.ListMineAsync(userId, status);
        var dto = list.Select(r => new ReservationDto(
            r.Id, r.StationId, r.BatteryModelId, r.BatteryUnitId,
            r.Status.ToString(), r.StartTime, r.HoldDurationMinutes, r.CreatedAt));
        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            await _service.CancelAsync(userId, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = new { code = "RESERVATION_NOT_FOUND", message = "Reservation not found" } });
        }
    }
}
