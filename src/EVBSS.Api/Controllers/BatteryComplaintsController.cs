using EVBSS.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using EVBSS.Api.Dtos.Complaints;
using Microsoft.AspNetCore.Authorization;
using EVBSS.Api.Extensions;
using System.Linq;

namespace EVBSS.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Staff")]
    public class BatteryComplaintsController : ControllerBase
    {
        private readonly BatteryComplaintService _complaintService;
        private readonly ILogger<BatteryComplaintsController> _logger;

        public BatteryComplaintsController(BatteryComplaintService complaintService, ILogger<BatteryComplaintsController> logger)
        {
            _complaintService = complaintService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetComplaints([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var complaints = await _complaintService.GetComplaintsAsync(page, pageSize);

            var items = complaints
                .Select(c => new EVBSS.Api.Dtos.Complaints.BatteryComplaintResponse
                {
                    Id = c.Id,
                    SwapTransactionId = c.SwapTransactionId,
                    IssuedBatteryId = c.IssuedBatteryId,
                    ReportedByUserId = c.ReportedByUserId,
                    Status = c.Status,
                    ComplaintDetails = c.ComplaintDetails,
                    ReportDate = c.ReportDate,
                    HandledByStaffId = c.HandledByStaffId,
                    ResolutionNotes = c.ResolutionNotes,
                    ResolvedAt = c.ResolvedAt,
                    IssuedBatterySerial = c.IssuedBattery?.Serial,
                    StationName = c.SwapTransaction?.Station?.Name
                })
                .ToList();

            return Ok(new { page, pageSize, items });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetComplaintById(Guid id)
        {
            try
            {
                var complaint = await _complaintService.GetComplaintByIdAsync(id);
                return Ok(complaint);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/resolve")]
        public async Task<IActionResult> ResolveComplaint(Guid id, [FromBody] ResolveComplaintRequest request)
        {
            try
            {
                var staffId = User.GetRequiredUserId();
                var complaint = await _complaintService.ResolveComplaintAsync(staffId, id, request);
                return Ok(complaint);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/finalize-reswap")]
        public async Task<IActionResult> FinalizeReswap(Guid id, [FromQuery] Guid stationId, [FromBody] CompleteReswapRequest request)
        {
            try
            {
                var staffId = User.GetRequiredUserId();

                // Gọi phương thức gộp 2 bước trong Service
                var swap = await _complaintService.ProcessAndCompleteReswapAsync(staffId, id, stationId, request);

                return Ok(new
                {
                    message = $"Giao dịch đổi pin miễn phí (Re-swap) cho khiếu nại {id} đã hoàn tất thành công. Pin cũ đã thu hồi, pin mới đã cấp. Khiếu nại đã được ĐÓNG (Resolved).",
                    SwapId = swap.Id,
                    IssuedBatterySerial = swap.IssuedBatterySerial,
                    ReceivedBatterySerial = swap.ReturnedBatterySerial,
                    swap.Status
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                // Ví dụ: Reservation chưa CheckIn, hoặc không có pin tốt
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing re-swap for complaint {ComplaintId}", id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi xử lý Re-swap.", details = ex.Message });
            }
        }

        // Finalization of complaints is handled automatically by the SwapTransaction workflow
        // when a related re-swap completes. Manual finalize endpoint removed to enforce
        // consistent process and avoid accidental state changes.

        [HttpPost("{id}/investigate")]
        public async Task<IActionResult> InvestigateComplaint(Guid id, [FromBody] InvestigateComplaintRequest request)
        {
            try
            {
                var staffId = User.GetRequiredUserId();
                var complaint = await _complaintService.InvestigateComplaintAsync(staffId, id, request);
                return Ok(new { message = $"Khiếu nại {complaint.Id} đã được chuyển sang trạng thái Investigating.", status = complaint.Status.ToString() });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    }
}
