using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EVBSS.Api.Dtos.Reservations;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/slot-reservations")]
[Authorize]
public class SlotReservationsController : ControllerBase
{
    private readonly SlotReservationService _service;
    
    public SlotReservationsController(SlotReservationService service)
    {
        _service = service;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out userId);
    }

    /// <summary>
    /// Xem các slot còn trống trong ngày
    /// </summary>
    [HttpGet("available-slots")]
    [ProducesResponseType(typeof(IEnumerable<SlotAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSlots(
        [FromQuery] Guid stationId,
        [FromQuery] DateTime date,
        [FromQuery] Guid batteryModelId)
    {
        var slots = await _service.GetAvailableSlotsAsync(stationId, date, batteryModelId);
        return Ok(slots);
    }

    /// <summary>
    /// Tạo reservation theo slot
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SlotReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSlotReservationRequest req)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var reservation = await _service.CreateReservationAsync(
                userId,
                req.StationId,
                req.BatteryModelId,
                req.SlotDate,
                req.SlotStartTime,
                req.SlotEndTime);

            var (earliestCheckIn, latestCheckIn) = ReservationSlotConfig.GetCheckInWindow(
                reservation.SlotDate,
                reservation.SlotStartTime,
                reservation.SlotEndTime);

            var response = new SlotReservationResponse
            {
                ReservationId = reservation.Id,
                Status = reservation.Status.ToString(),
                SlotDate = reservation.SlotDate,
                SlotStartTime = reservation.SlotStartTime,
                SlotEndTime = reservation.SlotEndTime,
                QRCode = reservation.QRCode!,
                CheckInWindow = new CheckInWindowDto
                {
                    EarliestTime = earliestCheckIn,
                    LatestTime = latestCheckIn
                }
            };

            return CreatedAtAction(nameof(GetById), new { id = reservation.Id }, response);
        }
        catch (ActiveReservationExistsException ex)
        {
            return BadRequest(new { error = new { code = "ACTIVE_RESERVATION_EXISTS", message = ex.Message } });
        }
        catch (SlotNotAvailableException ex)
        {
            return BadRequest(new { error = new { code = "SLOT_NOT_AVAILABLE", message = ex.Message } });
        }
    }

    /// <summary>
    /// Xem chi tiết reservation
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SlotReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        // TODO: Implement get by ID
        return NotFound();
    }

    /// <summary>
    /// Staff check-in driver bằng QR Code
    /// </summary>
    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(CheckInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest req)
    {
        if (!TryGetUserId(out var staffId))
            return Unauthorized();

        try
        {
            var reservation = await _service.CheckInAsync(id, req.QRCodeData, staffId);

            var response = new CheckInResponse
            {
                ReservationId = reservation.Id,
                Status = reservation.Status.ToString(),
                CheckedInAt = reservation.CheckedInAt!.Value,
                AssignedBattery = new AssignedBatteryDto
                {
                    BatteryId = reservation.BatteryUnitId!.Value,
                    Serial = reservation.BatteryUnit?.Serial ?? "N/A"
                }
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
        }
        catch (InvalidCheckInTimeException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_CHECKIN_TIME", message = ex.Message } });
        }
        catch (NoBatteryException ex)
        {
            return BadRequest(new { error = new { code = "NO_BATTERY", message = ex.Message } });
        }
    }

    /// <summary>
    /// Hủy reservation
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelReservationRequest? req)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var isStaff = User.IsInRole("Staff") || User.IsInRole("Admin");
            var reason = req?.Reason ?? CancelReason.UserCancelled;
            
            await _service.CancelReservationAsync(id, userId, reason, req?.Note, isStaff);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = new { code = "RESERVATION_NOT_FOUND", message = "Reservation not found" } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
        }
    }
}

// ===== DTOs =====

public record CreateSlotReservationRequest(
    Guid StationId,
    Guid BatteryModelId,
    DateTime SlotDate,
    TimeSpan SlotStartTime,
    TimeSpan SlotEndTime
);

public record SlotReservationResponse
{
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime SlotDate { get; set; }
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
    public string QRCode { get; set; } = null!;
    public CheckInWindowDto CheckInWindow { get; set; } = null!;
}

public record CheckInWindowDto
{
    public DateTime EarliestTime { get; set; }
    public DateTime LatestTime { get; set; }
}

public record CheckInRequest(string QRCodeData);

public record CheckInResponse
{
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CheckedInAt { get; set; }
    public AssignedBatteryDto AssignedBattery { get; set; } = null!;
}

public record AssignedBatteryDto
{
    public Guid BatteryId { get; set; }
    public string Serial { get; set; } = null!;
}

public record CancelReservationRequest(
    CancelReason Reason,
    string? Note
);
