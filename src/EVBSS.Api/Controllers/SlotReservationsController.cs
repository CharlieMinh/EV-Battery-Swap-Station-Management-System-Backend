using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
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
        [FromQuery] DateOnly date,  // UPDATED: Changed from DateTime to DateOnly
        [FromQuery] Guid batteryModelId)
    {
        var slots = await _service.GetAvailableSlotsAsync(stationId, date, batteryModelId);
        return Ok(slots);
    }

    /// <summary>
    /// Xem các slot còn trống trong ngày cho việc đặt lịch kiểm tra pin (từ khiếu nại)
    /// </summary>
    [HttpGet("inspection-slots")]
    [ProducesResponseType(typeof(IEnumerable<SlotAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailableInspectionSlots(
        [FromQuery] Guid stationId,
        [FromQuery] DateOnly date,
        [FromQuery] Guid complaintId)
    {
        try
        {
            var slots = await _service.GetAvailableInspectionSlotsAsync(stationId, date, complaintId);
            return Ok(slots);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "COMPLAINT_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_COMPLAINT_STATE", message = ex.Message } });
        }
    }

    /// <summary>
    /// Xem danh sách reservations (Admin/Staff Dashboard)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(IEnumerable<SlotReservationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReservations(
        [FromQuery] DateTime? date,
        [FromQuery] Guid? stationId,
        [FromQuery] ReservationStatus? status,
        [FromQuery] Guid? userId)
    {
        var reservations = await _service.GetReservationsAsync(date, stationId, status, userId);
        var response = reservations.Select(MapToResponse);
        return Ok(response);
    }

    /// <summary>
    /// Xem reservations của mình (Customer)
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<SlotReservationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReservations([FromQuery] ReservationStatus? status)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var reservations = await _service.GetReservationsAsync(userId: userId, status: status);
        var response = reservations.Select(MapToResponse);
        return Ok(response);
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
                req.VehicleId,
                req.SlotDate,
                req.SlotStartTime,
                req.SlotEndTime,
                req.PaymentMethod);  // ⭐ FIXED 2025-10-25: Pass PaymentMethod for pay-per-swap

            var response = MapToResponse(reservation);

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
        catch (NoActiveSubscriptionException ex)
        {
            return BadRequest(new { error = new { code = "NO_ACTIVE_SUBSCRIPTION", message = ex.Message } });
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

        try
        {
            // Check if user is Staff or Admin
            var isStaffOrAdmin = User.IsInRole("Staff") || User.IsInRole("Admin");
            
            var reservation = await _service.GetReservationByIdAsync(id, userId, isStaffOrAdmin);
            return Ok(MapToResponse(reservation));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = new { code = "RESERVATION_NOT_FOUND", message = "Không tìm thấy lịch đặt" } });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Staff check-in driver bằng QR Code
    /// </summary>
    [HttpPost("{id:guid}/check-in")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(CheckInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentPendingCashResponse), StatusCodes.Status400BadRequest)]
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
        catch (PaymentPendingCashException ex)
        {
            return BadRequest(new PaymentPendingCashResponse
            {
                Error = new ErrorResponse { Code = "PAYMENT_PENDING_CASH", Message = ex.Message },
                PaymentId = ex.PaymentId,
                Amount = ex.Amount
            });
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

    // Helper method to map Reservation to Response DTO
    private SlotReservationResponse MapToResponse(Reservation reservation)
    {
        // Convert DateOnly + TimeSpan to DateTime for check-in window calculation
        var slotDateTime = reservation.SlotDate.ToDateTime(TimeOnly.FromTimeSpan(reservation.SlotStartTime));
        var (earliest, latest) = ReservationSlotConfig.GetCheckInWindow(
            slotDateTime, 
            reservation.SlotStartTime, 
            ReservationSlotConfig.CheckInBuffer
        );

        // ⭐ NEW: Extract vehicle information
        var vehicleName = reservation.Vehicle?.VehicleModel?.Name ?? "Unknown";
        var licensePlate = reservation.Vehicle?.Plate ?? "Unknown";

        return new SlotReservationResponse
        {
            Id = reservation.Id,
            ReservationId = reservation.Id,  // For backward compatibility
            StationId = reservation.StationId,
            StationName = reservation.Station?.Name ?? "Unknown",
            BatteryModelId = reservation.BatteryModelId,
            BatteryModelName = reservation.BatteryModel?.Name ?? "Unknown",
            Status = reservation.Status.ToString(),
            SlotDate = reservation.SlotDate,
            SlotStartTime = reservation.SlotStartTime,
            SlotEndTime = reservation.SlotEndTime,
            QRCode = reservation.QRCode ?? "",
            CheckInWindow = new CheckInWindowDto
            {
                EarliestTime = earliest,
                LatestTime = latest
            },
            UserId = reservation.UserId,
            RelatedComplaintId = reservation.RelatedComplaintId,
            
            // ⭐ NEW: Vehicle information
            VehicleId = reservation.VehicleId,
            VehicleName = vehicleName,
            LicensePlate = licensePlate
        };
    }
}

// ===== DTOs =====

public record CreateSlotReservationRequest(
    Guid StationId,
    Guid VehicleId,
    DateOnly SlotDate,  // UPDATED: Changed from DateTime to DateOnly to fix timezone issue
    TimeSpan SlotStartTime,
    TimeSpan SlotEndTime,
    PaymentMethod? PaymentMethod = null  // ⭐ NEW 2025-10-25: If null → Use subscription (free), if set → Pay-per-swap
);

public record SlotReservationResponse
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }  // Backward compatibility
    public Guid StationId { get; set; }
    public string StationName { get; set; } = null!;
    public Guid BatteryModelId { get; set; }
    public string BatteryModelName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly SlotDate { get; set; }  // UPDATED: Changed from DateTime to DateOnly
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
    public string QRCode { get; set; } = null!;
    public CheckInWindowDto CheckInWindow { get; set; } = null!;
    public Guid UserId { get; set; }
    public Guid? RelatedComplaintId { get; set; }  // Complaint that triggered this reservation
    
    // ⭐ NEW 2025-11-07: Vehicle information for Staff to display
    public Guid? VehicleId { get; set; }
    public string? VehicleName { get; set; }        // Vehicle model name (e.g., "VinFast VF8")
    public string? LicensePlate { get; set; }       // License plate number (e.g., "30A-12345")
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

// --- DTOs for Check-In Error Handling ---

public record PaymentPendingCashResponse
{
    [JsonPropertyName("error")]
    public ErrorResponse Error { get; set; } = null!;
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; set; }
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public record ErrorResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;
    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;
}