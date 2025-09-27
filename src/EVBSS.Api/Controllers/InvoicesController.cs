using EVBSS.Api.Dtos.Invoices;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(IInvoiceService invoiceService, ILogger<InvoicesController> logger)
    {
        _invoiceService = invoiceService;
        _logger = logger;
    }

    /// <summary>
    /// Xem danh sách hóa đơn của user
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetMyInvoices()
    {
        try
        {
            var userId = GetCurrentUserId();
            var invoices = await _invoiceService.GetUserInvoicesAsync(userId);
            
            return Ok(invoices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user invoices");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy danh sách hóa đơn." });
        }
    }

    /// <summary>
    /// Xem chi tiết hóa đơn
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoiceById(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id, userId);
            
            if (invoice == null)
            {
                return NotFound(new { message = "Không tìm thấy hóa đơn hoặc hóa đơn không thuộc về bạn." });
            }
            
            return Ok(invoice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice {InvoiceId}", id);
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy thông tin hóa đơn." });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }
}