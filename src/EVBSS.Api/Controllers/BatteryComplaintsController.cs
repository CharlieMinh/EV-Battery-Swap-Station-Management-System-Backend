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

        [HttpPost("{id}/receive-faulty-battery")]
        public async Task<IActionResult> ReceiveFaultyBatteryAndCreateReswap(Guid id)
        {
            try
            {
                var staffId = User.GetRequiredUserId();
                var reservation = await _complaintService.ProcessFaultyBatteryReturnAndCreateReswapAsync(staffId, id);
                return Ok(new { message = "Pin lỗi đã được thu hồi và một lượt đổi pin miễn phí đã được tạo.", reservation });
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

        [HttpPost("{id}/finalize")]
        public async Task<IActionResult> FinalizeComplaint(Guid id)
        {
            try
            {
                var staffId = User.GetRequiredUserId();
                var result = await _complaintService.FinalizeComplaintAsync(staffId, id);

                return Ok(new
                {
                    message = $"Khiếu nại {result.Id} đã được đóng thành công (Resolved).",
                    status = result.Status.ToString()
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing complaint {ComplaintId}", id);
                return StatusCode(500, new { error = "Có lỗi xảy ra khi hoàn tất khiếu nại." });
            }
        }

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
