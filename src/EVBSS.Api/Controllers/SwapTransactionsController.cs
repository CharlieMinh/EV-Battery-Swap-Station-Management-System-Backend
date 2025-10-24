using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EVBSS.Api.Services;
using EVBSS.Api.Dtos.SwapTransactions;
using EVBSS.Api.Models;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/swaps")]
[Authorize]
public class SwapTransactionsController : ControllerBase
{
    private readonly SwapTransactionService _swapService;
    private readonly ILogger<SwapTransactionsController> _logger;

    public SwapTransactionsController(
        SwapTransactionService swapService,
        ILogger<SwapTransactionsController> logger)
    {
        _swapService = swapService;
        _logger = logger;
    }

    /// <summary>
    /// ⭐ LUỒNG 4: Hoàn tất giao dịch đổi pin từ một reservation đã check-in.
    /// </summary>
    [HttpPost("finalize-from-reservation")]
    [Authorize(Roles = "Staff,Admin")]
    [ProducesResponseType(typeof(FinalizeSwapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FinalizeSwapResponse>> FinalizeFromReservation([FromBody] FinalizeSwapRequest request)
    {
        try
        {
            var staffId = GetCurrentUserId();
            var transaction = await _swapService.FinalizeFromReservationAsync(request, staffId);

            var response = new FinalizeSwapResponse
            {
                Success = true,
                SwapTransactionId = transaction.Id,
                Message = "Giao dịch đổi pin đã hoàn tất thành công."
            };

            _logger.LogInformation("Swap transaction {TransactionId} finalized from reservation {ReservationId} by staff {StaffId}", 
                transaction.Id, request.ReservationId, staffId);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Failed to finalize swap from reservation {ReservationId}: {Error}", request.ReservationId, ex.Message);
            return NotFound(new FinalizeSwapResponse { Success = false, Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to finalize swap from reservation {ReservationId}: {Error}", request.ReservationId, ex.Message);
            return BadRequest(new FinalizeSwapResponse { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing swap from reservation {ReservationId}", request.ReservationId);
            return StatusCode(500, new FinalizeSwapResponse { Success = false, Message = "Đã có lỗi xảy ra khi hoàn tất giao dịch." });
        }
    }

    // ... All other existing methods like StartSwap, IssueBattery, etc. are kept for other flows ...

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }

    private SwapTransactionResponse MapToResponse(SwapTransaction swap)
    {
        return new SwapTransactionResponse
        {
            Id = swap.Id,
            TransactionNumber = swap.TransactionNumber,
            Status = swap.Status.ToString(),
            UserEmail = swap.User?.Email ?? User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            StationName = swap.Station?.Name ?? "",
            StationAddress = swap.Station?.Address ?? "",
            VehicleLicensePlate = swap.Vehicle?.Plate ?? "",
            VehicleModel = swap.Vehicle?.VIN ?? "",
            VehicleOdoAtSwap = swap.VehicleOdoAtSwap,
            IssuedBatterySerial = swap.IssuedBatterySerial,
            ReturnedBatterySerial = swap.ReturnedBatterySerial,
            BatteryHealthIssued = swap.BatteryHealthIssued,
            BatteryHealthReturned = swap.BatteryHealthReturned,
            PaymentType = swap.PaymentType.ToString(),
            SwapFee = swap.SwapFee,
            KmChargeAmount = swap.KmChargeAmount,
            TotalAmount = swap.TotalAmount,
            IsPaid = swap.IsPaid,
            StartedAt = swap.StartedAt,
            CheckedInAt = swap.CheckedInAt,
            BatteryIssuedAt = swap.BatteryIssuedAt,
            BatteryReturnedAt = swap.BatteryReturnedAt,
            CompletedAt = swap.CompletedAt,
            Notes = swap.Notes,
            ReservationId = swap.ReservationId,
            UserSubscriptionId = swap.UserSubscriptionId,
            Rating = swap.Rating,
            Feedback = swap.Feedback,
            RatedAt = swap.RatedAt
        };
    }
}
