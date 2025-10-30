using EVBSS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EVBSS.Api.Dtos.Complaints;
using EVBSS.Api.Extensions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using EVBSS.Api.Data; // <--- Thêm using AppDbContext
using Microsoft.EntityFrameworkCore; // <--- Thêm using EFCore
using System.Linq; // <--- Thêm using Linq
using Microsoft.AspNetCore.Http;

namespace EVBSS.Api.Controllers
{
    [Route("api/driver/complaints")]
    [ApiController]
    [Authorize(Roles = "Driver")]
    public class DriverBatteryComplaintsController : ControllerBase
    {
        private readonly BatteryComplaintService _complaintService;
        private readonly AppDbContext _context; // <--- Inject AppDbContext

        public DriverBatteryComplaintsController(BatteryComplaintService complaintService, AppDbContext context) // <--- Cập nhật Constructor
        {
            _complaintService = complaintService;
            _context = context; // <--- Gán AppDbContext
        }

        private Guid GetRequiredUserId()
        {
            return User.GetRequiredUserId();
        }

        /// <summary>
        /// Driver báo cáo pin lỗi cho một SwapTransaction.
        /// </summary>
        // ... (phần này giữ nguyên)
        [HttpPost("report")]
        public async Task<IActionResult> ReportFaultyBattery([FromBody] ReportFaultyBatteryRequest request)
        {
            try
            {
                var driverId = GetRequiredUserId();
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
        /// ⭐ NEW API: Driver xem danh sách tất cả khiếu nại của bản thân.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<BatteryComplaintResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyComplaints()
        {
            var driverId = GetRequiredUserId();

            var complaints = await _context.BatteryComplaints
                .Include(c => c.SwapTransaction)
                    .ThenInclude(s => s!.Station) // Thêm dấu ! để loại bỏ warning khi dùng ThenInclude
                .Include(c => c.IssuedBattery)
                .Where(c => c.ReportedByUserId == driverId)
                .OrderByDescending(c => c.ReportDate)
                .Select(c => new BatteryComplaintResponse
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
                    IssuedBatterySerial = c.IssuedBattery != null ? c.IssuedBattery.Serial : null,
                    StationName = c.SwapTransaction != null && c.SwapTransaction.Station != null ? c.SwapTransaction.Station.Name : null
                })
                .ToListAsync();

            return Ok(complaints);
        }

        /// <summary>
        /// Driver đặt lịch kiểm tra ban đầu cho khiếu nại (chuyển status Complaint: PendingScheduling -> Scheduled).
        /// VehicleId không cần thiết trong body vì được suy ra từ ComplaintId.
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

                var driverId = GetRequiredUserId();
                // DTO mới đã không cần VehicleId trong body
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
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetComplaintById([FromRoute] Guid id)
        {
            try
            {
                var driverId = GetRequiredUserId();
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