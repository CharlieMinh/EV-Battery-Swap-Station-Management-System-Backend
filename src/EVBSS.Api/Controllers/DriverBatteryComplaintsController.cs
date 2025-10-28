using EVBSS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EVBSS.Api.Dtos.Complaints;
using EVBSS.Api.Extensions;
using System;
using System.Threading.Tasks;

namespace EVBSS.Api.Controllers
{
    [Route("api/driver/complaints")]
    [ApiController]
    [Authorize(Roles = "Driver")]
    public class DriverBatteryComplaintsController : ControllerBase
    {
        private readonly BatteryComplaintService _complaintService;

        public DriverBatteryComplaintsController(BatteryComplaintService complaintService)
        {
            _complaintService = complaintService;
        }

        /// <summary>
        /// Driver báo cáo pin lỗi cho một SwapTransaction.
        /// </summary>
        [HttpPost("report")]
        public async Task<IActionResult> ReportFaultyBattery([FromBody] ReportFaultyBatteryRequest request)
        {
            try
            {
                var driverId = User.GetRequiredUserId();
                var complaint = await _complaintService.ReportFaultyBatteryAsync(driverId, request);

                return CreatedAtAction("GetComplaintById", new { id = complaint.Id }, new
                {
                    message = $"Khiếu nại số {complaint.Id} đã được tạo thành công.",
                    complaint.Id,
                    complaint.Status
                });
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

        /// <summary>
        /// Driver xem chi tiết khiếu nại của mình.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetComplaintById(Guid id)
        {
            try
            {
                var driverId = User.GetRequiredUserId();
                var complaint = await _complaintService.GetComplaintByIdAsync(id);

                if (complaint.ReportedByUserId != driverId)
                {
                    return Forbid();
                }

                return Ok(complaint);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Driver đặt lịch đổi pin miễn phí (Re-swap) cho khiếu nại đã được Confirmed.
        /// </summary>
        [HttpPost("{complaintId}/book-reswap")]
        public async Task<IActionResult> BookReswapReservation([FromRoute] Guid complaintId, [FromBody] CreateReswapReservationRequest request)
        {
            try
            {
                if (complaintId != request.ComplaintId)
                {
                    return BadRequest(new { message = "ID khiếu nại trong đường dẫn không khớp với ID trong Body." });
                }

                var driverId = User.GetRequiredUserId();
                var reservation = await _complaintService.DriverCreateReswapReservationAsync(driverId, request);

                return CreatedAtAction("GetComplaintById", new { id = complaintId }, new
                {
                    message = $"Lịch hẹn đổi pin miễn phí {reservation.Id} đã được đặt thành công. Vui lòng Check-in tại trạm đúng giờ. Mã QR đã được tạo.",
                    reservation.Id,
                    reservation.Status,
                    reservation.QRCode
                });
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
