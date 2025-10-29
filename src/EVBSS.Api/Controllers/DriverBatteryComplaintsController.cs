using EVBSS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EVBSS.Api.Dtos.Complaints;
using EVBSS.Api.Extensions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic; // Added for KeyNotFoundException

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

                // FIX: Giờ đây sẽ trỏ đến phương thức GetComplaintById mới trong cùng Controller
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

        // NOTE: The legacy "book-reswap" endpoint has been removed in favor of the
        // single initial inspection scheduling flow (POST {complaintId}/schedule-inspection).
        // If you need to reintroduce a reservation-based re-swap flow, implement it
        // against the new inspection scheduling and finalize flows in the service layer.

        /// <summary>
        /// Driver đặt lịch kiểm tra ban đầu cho khiếu nại (chuyển status Complaint: PendingScheduling -> Scheduled).
        /// </summary>
        [HttpPost("{complaintId}/schedule-inspection")]
        public async Task<IActionResult> ScheduleInspectionReservation([FromRoute] Guid complaintId, [FromBody] CreateInspectionReservationRequest request)
        {
            try
            {
                if (complaintId != request.ComplaintId)
                {
                    return BadRequest(new { message = "ID khiếu nại trong đường dẫn không khớp với ID trong Body." });
                }

                var driverId = User.GetRequiredUserId();
                var reservation = await _complaintService.DriverScheduleInitialInspectionAsync(driverId, request);

                return CreatedAtAction("GetComplaintById", new { id = complaintId }, new
                {
                    message = $"Lịch hẹn kiểm tra {reservation.Id} đã được đặt thành công. Khiếu nại đã chuyển sang trạng thái Scheduled.",
                    ReservationId = reservation.Id,
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
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Driver lấy chi tiết khiếu nại của chính mình theo ID.
        /// Thao tác này là cần thiết để hỗ trợ CreatedAtAction trong ReportFaultyBattery.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetComplaintById([FromRoute] Guid id)
        {
            try
            {
                var driverId = User.GetRequiredUserId();
                // Lấy chi tiết complaint (sử dụng service chung)
                var complaint = await _complaintService.GetComplaintByIdAsync(id);
                
                // Kiểm tra quyền: Đảm bảo Driver chỉ có thể xem khiếu nại của chính họ
                if (complaint.ReportedByUserId != driverId)
                {
                    // Trả về 404 NotFound để không tiết lộ sự tồn tại của ID cho người dùng không có quyền.
                    return NotFound(new { message = "Không tìm thấy khiếu nại hoặc khiếu nại không thuộc về bạn." });
                }
                
                return Ok(complaint);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
