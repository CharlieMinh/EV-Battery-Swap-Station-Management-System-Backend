using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using EVBSS.Api.Services;
using EVBSS.Api.Dtos.SwapTransactions;
using EVBSS.Api.Models;

namespace EVBSS.Api.Controllers;

/// <summary>
/// Controller xử lý các API giao dịch đổi pin
/// Quản lý toàn bộ quy trình từ bắt đầu đến hoàn thành việc đổi pin
/// </summary>
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
    /// Bắt đầu giao dịch đổi pin từ đặt chỗ hiện có
    /// </summary>
    /// <param name="request">Chi tiết yêu cầu bắt đầu đổi pin</param>
    /// <returns>Thông tin giao dịch đổi pin đã bắt đầu</returns>
    [HttpPost("start")]
    public async Task<ActionResult<SwapTransactionResponse>> StartSwap([FromBody] StartSwapRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var swap = await _swapService.StartSwapFromReservationAsync(userId, request);

            var response = MapToResponse(swap);

            _logger.LogInformation("Giao dịch đổi pin đã bắt đầu: {TransactionNumber} bởi user {UserId}", 
                swap.TransactionNumber, userId);

            return CreatedAtAction(nameof(GetSwapById), new { id = swap.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Không thể bắt đầu đổi pin cho user {UserId}: {Error}", GetCurrentUserId(), ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi bắt đầu đổi pin cho user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { error = "Đã xảy ra lỗi khi bắt đầu giao dịch đổi pin" });
        }
    }

    /// <summary>
    /// Hoàn thành giao dịch đổi pin
    /// </summary>
    /// <param name="id">ID giao dịch đổi pin</param>
    /// <param name="request">Chi tiết yêu cầu hoàn thành đổi pin</param>
    /// <returns>Thông tin giao dịch đổi pin đã hoàn thành</returns>
    [HttpPut("{id}/complete")]
    public async Task<ActionResult<SwapTransactionResponse>> CompleteSwap(
        [FromRoute] Guid id, 
        [FromBody] CompleteSwapRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var swap = await _swapService.CompleteSwapAsync(id, userId, request);

            var response = MapToResponse(swap);

            _logger.LogInformation("Battery swap completed: {TransactionNumber} by user {UserId}", 
                swap.TransactionNumber, userId);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to complete swap {SwapId} for user {UserId}: {Error}", 
                id, GetCurrentUserId(), ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing swap {SwapId} for user {UserId}", id, GetCurrentUserId());
            return StatusCode(500, new { error = "An error occurred while completing the swap" });
        }
    }

    /// <summary>
    /// Lấy lịch sử giao dịch đổi pin của người dùng với phân trang
    /// </summary>
    /// <param name="page">Số trang (mặc định: 1)</param>
    /// <param name="pageSize">Số item mỗi trang (mặc định: 10, tối đa: 50)</param>
    /// <returns>Lịch sử đổi pin có phân trang</returns>
   [HttpGet("mine")]
public async Task<ActionResult<SwapHistoryResponse>> GetMySwapHistory(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 3,        // default nhỏ (3) để load nhanh ở FE
    [FromQuery] bool all = false)        // nếu true => trả toàn bộ (có giới hạn)
{
    try
    {
        if (page < 1) page = 1;

        // Giới hạn an toàn để tránh trả quá nhiều bản ghi (bảo vệ server)
        const int MaxFetchLimit = 1000;

        var userId = GetCurrentUserId();

        if (all)
        {
            // Lấy tổng số bản ghi (GetUserSwapHistoryAsync đã trả về TotalCount)
            // Gọi 1 lần để biết totalCount (chỉ lấy pageSize=1 để giảm dữ liệu không cần thiết)
            var tmp = await _swapService.GetUserSwapHistoryAsync(userId, 1, 1);
            var totalCount = tmp.TotalCount;

            // Nếu không có record thì trả response rỗng 
            if (totalCount == 0)
            {
                return Ok(new SwapHistoryResponse
                {
                    Transactions = new List<SwapTransactionResponse>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = 0,
                    TotalPages = 0
                });
            }

            // Giới hạn số bản ghi trả về tối đa để tránh quá tải
            var fetchSize = Math.Min(totalCount, MaxFetchLimit);

            var resultAll = await _swapService.GetUserSwapHistoryAsync(userId, 1, fetchSize);

            _logger.LogInformation("Retrieved ALL swap history for user {UserId}: {Count} transactions (capped at {Cap})",
                userId, resultAll.TotalCount, MaxFetchLimit);

            return Ok(resultAll);
        }

       
        if (pageSize < 1) pageSize = 3;
        if (pageSize > MaxFetchLimit) pageSize = MaxFetchLimit;

        var result = await _swapService.GetUserSwapHistoryAsync(userId, page, pageSize);

        _logger.LogInformation("Retrieved swap history for user {UserId}: {Count} transactions (page {Page}, size {Size})",
            userId, result.TotalCount, page, pageSize);

        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving swap history for user {UserId}", GetCurrentUserId());
        return StatusCode(500, new { error = "An error occurred while retrieving swap history" });
    }
}


    /// <summary>
    /// Get a specific swap transaction by ID
    /// </summary>
    /// <param name="id">Swap transaction ID</param>
    /// <returns>Swap transaction details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<SwapTransactionResponse>> GetSwapById([FromRoute] Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _swapService.GetUserSwapHistoryAsync(userId, 1, 1000); // Get all to find by ID
            
            var swap = result.Transactions.FirstOrDefault(t => t.Id == id);
            if (swap == null)
            {
                return NotFound(new { error = "Swap transaction not found" });
            }

            return Ok(swap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving swap {SwapId} for user {UserId}", id, GetCurrentUserId());
            return StatusCode(500, new { error = "An error occurred while retrieving the swap transaction" });
        }
    }

    /// <summary>
    /// Get current swap transaction status (if any in progress)
    /// </summary>
    /// <returns>Current active swap transaction or null</returns>
    [HttpGet("current")]
    public async Task<ActionResult<SwapTransactionResponse?>> GetCurrentSwap()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _swapService.GetUserSwapHistoryAsync(userId, 1, 50);
            
            // Find any in-progress swap (CheckedIn status)
            var currentSwap = result.Transactions
                .FirstOrDefault(t => t.Status == SwapTransactionStatus.CheckedIn.ToString());

            if (currentSwap == null)
            {
                return Ok(null);
            }

            return Ok(currentSwap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current swap for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { error = "An error occurred while retrieving current swap status" });
        }
    }

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
            UserSubscriptionId = swap.UserSubscriptionId
        };
    }
}