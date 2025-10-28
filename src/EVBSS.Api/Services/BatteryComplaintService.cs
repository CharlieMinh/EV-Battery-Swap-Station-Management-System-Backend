using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.Complaints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using EVBSS.Api.Hubs;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

namespace EVBSS.Api.Services
{
    public class BatteryComplaintService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BatteryComplaintService> _logger;
        private readonly IBatteryInventoryService _inventoryService;
    private readonly ReservationService? _reservationService;
    private readonly IHubContext<NotificationHub>? _hubContext;
    private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        private string QRSecret => _config["QRCode:SecretKey"] ?? "DEFAULT_SECRET_KEY_CHANGE_ME";

        public BatteryComplaintService(
            AppDbContext context,
            ILogger<BatteryComplaintService> logger,
            IBatteryInventoryService inventoryService,
            IConfiguration config,
            IServiceProvider serviceProvider,
            ReservationService? reservationService = null,
            IHubContext<NotificationHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _inventoryService = inventoryService;
            _config = config;
            _reservationService = reservationService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
        }

        public async Task<BatteryComplaint> InvestigateComplaintAsync(Guid staffId, Guid complaintId, InvestigateComplaintRequest request)
        {
            var complaint = await _context.BatteryComplaints
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

            if (complaint.Status != ComplaintStatus.Pending)
                throw new InvalidOperationException($"Chỉ có thể chuyển sang Investigating từ trạng thái Pending. Trạng thái hiện tại: {complaint.Status}.");

            complaint.Status = ComplaintStatus.Investigating;
            complaint.HandledByStaffId = staffId;
            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty) + "\n[Investigating Note]: " + (request.InvestigationNotes ?? string.Empty);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Complaint {ComplaintId} marked as Investigating by staff {StaffId}", complaintId, staffId);

            return complaint;
        }

        public async Task<List<BatteryComplaint>> GetComplaintsAsync(int page = 1, int pageSize = 20)
        {
            var skip = Math.Max(0, (page - 1) * pageSize);
            return await _context.BatteryComplaints
                // Eager-load properties used by controller/UI to avoid N+1 and null navs
                .Include(c => c.IssuedBattery)
                .Include(c => c.SwapTransaction)
                    .ThenInclude(s => s.Station)
                .Include(c => c.ReportedByUser)
                .Include(c => c.HandledByStaff)
                .OrderByDescending(c => c.ReportDate)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<BatteryComplaint> GetComplaintByIdAsync(Guid id)
        {
            var complaint = await _context.BatteryComplaints
                .Include(c => c.SwapTransaction)
                    .ThenInclude(s => s.Station)
                .Include(c => c.IssuedBattery)
                    .ThenInclude(b => b.Model)
                .Include(c => c.ReportedByUser)
                .Include(c => c.HandledByStaff)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

            return complaint;
        }

        public async Task<BatteryComplaint> ReportFaultyBatteryAsync(Guid driverId, ReportFaultyBatteryRequest request)
        {
            var swap = await _context.SwapTransactions
                .Include(s => s.IssuedBattery)
                .FirstOrDefaultAsync(s => s.Id == request.SwapTransactionId && s.UserId == driverId);

            if (swap == null || swap.IssuedBattery == null)
                throw new KeyNotFoundException("Giao dịch đổi pin không hợp lệ hoặc không thuộc về bạn.");

            // Only allow reporting faulty batteries for completed swap transactions —
            // the battery is considered transferred to the driver only after completion.
            if (swap.Status != SwapTransactionStatus.Completed)
                throw new InvalidOperationException("Chỉ có thể báo cáo pin lỗi cho các giao dịch đổi pin đã hoàn tất.");

            var existingComplaint = await _context.BatteryComplaints
                .AnyAsync(c => c.SwapTransactionId == request.SwapTransactionId);

            if (existingComplaint)
                throw new InvalidOperationException("Một khiếu nại cho giao dịch này đã tồn tại.");

            var complaint = new BatteryComplaint
            {
                ReportedByUserId = driverId,
                SwapTransactionId = request.SwapTransactionId,
                IssuedBatteryId = swap.IssuedBatteryId,
                ComplaintDetails = request.ComplaintDetails,
                Status = ComplaintStatus.Pending,
                ReportDate = DateTime.UtcNow
            };

            _context.BatteryComplaints.Add(complaint);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New battery complaint {ComplaintId} created for swap {SwapId} by user {UserId}", complaint.Id, swap.Id, driverId);

            // Push real-time notification to staff group (if hub is available)
            try
            {
                if (_hubContext != null)
                {
                    var payload = new
                    {
                        ComplaintId = complaint.Id,
                        SwapId = swap.Id,
                        StationId = swap.StationId,
                        Message = "Người dùng đã báo pin lỗi. Vui lòng kiểm tra."
                    };
                    await _hubContext.Clients.Group("Staff").SendAsync("ReceiveComplaint", payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send realtime notification for complaint {ComplaintId}", complaint.Id);
            }

            return complaint;
        }

        public async Task<BatteryComplaint> ResolveComplaintAsync(Guid staffId, Guid complaintId, ResolveComplaintRequest request)
        {
            if (request.NewStatus != ComplaintStatus.Confirmed && request.NewStatus != ComplaintStatus.Rejected)
            {
                throw new ArgumentException("Trạng thái giải quyết không hợp lệ. Chỉ chấp nhận Confirmed hoặc Rejected.");
            }

            var complaint = await GetComplaintByIdAsync(complaintId);

            if (complaint.Status != ComplaintStatus.Pending && complaint.Status != ComplaintStatus.Investigating)
                throw new InvalidOperationException($"Không thể xử lý khiếu nại đã ở trạng thái {complaint.Status}.");

            complaint.Status = request.NewStatus;
            complaint.HandledByStaffId = staffId;
            // Append resolution notes instead of overwriting to preserve history
            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty)
                + $"\n[Decision Note - {request.NewStatus}]: " + (request.ResolutionNotes ?? string.Empty);

            string message;
            if (request.NewStatus == ComplaintStatus.Confirmed)
            {
                // Do not instruct the driver to go to a specific station here because
                // staff may choose a different station to receive the faulty battery.
                // Inform the driver that the complaint is confirmed and they should
                // check the 'Lịch hẹn' (Reservations) section for the re-swap details.
                message = $"Khiếu nại pin lỗi đã được XÁC NHẬN (Confirmed). Nhân viên sẽ tạo một lượt đổi pin miễn phí cho bạn tại trạm phù hợp. Vui lòng kiểm tra mục 'Lịch hẹn' để xem thông tin đổi pin mới.";
            }
            else // Rejected
            {
                message = $"Khiếu nại pin lỗi đã bị TỪ CHỐI. Pin không phát hiện lỗi hệ thống. Chi tiết: {request.ResolutionNotes}";
            }

            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = complaint.ReportedByUserId,
                SenderId = staffId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Type = NotificationType.Generic,
                RelatedEntityId = complaint.Id
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Complaint {ComplaintId} has been {Status} by staff {StaffId}", complaintId, request.NewStatus, staffId);

            return complaint;
        }

        public async Task<BatteryComplaint> ProcessFaultyBatteryReturnAsync(Guid staffId, Guid complaintId, Guid staffStationId, bool isChained = false)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var complaint = await GetComplaintByIdAsync(complaintId);

                if (complaint.Status != ComplaintStatus.Confirmed)
                    throw new InvalidOperationException($"Không thể thu hồi pin cho khiếu nại ở trạng thái {complaint.Status}. Driver phải đặt lịch đổi pin trước.");

                var issuedBattery = complaint.IssuedBattery
                    ?? throw new InvalidOperationException("Không tìm thấy thông tin pin bị lỗi.");

                var swap = complaint.SwapTransaction
                    ?? throw new InvalidOperationException("Không tìm thấy giao dịch đổi pin liên quan.");

                // Require that the driver previously created a reservation linked to this complaint
                // and that the driver has CheckedIn at the station before staff can receive the faulty battery.
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.RelatedComplaintId == complaint.Id);

                if (reservation == null)
                    throw new InvalidOperationException("Driver chưa đặt lịch hẹn đổi pin miễn phí (Re-swap).");

                if (reservation.Status != ReservationStatus.CheckedIn)
                    throw new InvalidOperationException($"Lịch hẹn Re-swap (ID: {reservation.Id}) chưa được Driver Check-in. Staff không thể thu hồi pin.");

                // Backfill link from original swap to complaint for auditability
                if (swap.RelatedComplaintId == null)
                {
                    swap.RelatedComplaintId = complaint.Id;
                }

                // Update battery status to Faulty and inventory (same as previous logic)
                var oldStatus = issuedBattery.Status;
                if (oldStatus != BatteryStatus.Faulty)
                {
                    var currentPhysicalStationId = issuedBattery.StationId;
                    var sourceStationId = swap.StationId;

                    issuedBattery.Status = BatteryStatus.Faulty;
                    issuedBattery.UpdatedAt = DateTime.UtcNow;

                    if (oldStatus == BatteryStatus.InUse)
                    {
                        await _inventoryService.ChangeInventoryCountByStatusAsync(
                            issuedBattery.BatteryModelId,
                            sourceStationId,
                            BatteryStatus.InUse,
                            -1);
                    }
                    else
                    {
                        await _inventoryService.ChangeInventoryCountByStatusAsync(
                            issuedBattery.BatteryModelId,
                            currentPhysicalStationId,
                            oldStatus,
                            -1);
                    }

                    issuedBattery.StationId = staffStationId;

                    await _inventoryService.ChangeInventoryCountByStatusAsync(
                        issuedBattery.BatteryModelId,
                        staffStationId,
                        BatteryStatus.Faulty,
                        1);
                }

                // Append a note that the faulty battery was received.
                complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty)
                    + $"\nPin lỗi đã được thu hồi bởi Staff {staffId} tại trạm {staffStationId}.";

                // Only create a user-facing notification when this is an independent API call.
                if (!isChained)
                {
                    _context.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = complaint.ReportedByUserId,
                        SenderId = staffId,
                        Message = $"Pin lỗi liên quan đến khiếu nại {complaint.Id} đã được Staff thu hồi. Vui lòng hoàn tất giao dịch đổi pin mới.",
                        Type = NotificationType.Generic,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        RelatedEntityId = complaint.Id
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully processed faulty battery return for complaint {ComplaintId} by staff {StaffId} (Chained: {IsChained})", complaintId, staffId, isChained);

                return complaint;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing faulty battery return for complaint {ComplaintId}", complaintId);
                throw;
            }
        }

        /// <summary>
        /// Completes the re-swap flow for a complaint by finalizing the reservation -> creating a SwapTransaction,
        /// issuing the replacement battery, updating inventories and then finalizing the complaint.
        /// This method delegates heavy-lifting to SwapTransactionService.FinalizeFromReservationAsync.
        /// </summary>
        public async Task<SwapTransaction> CompleteReswapTransactionAsync(Guid staffId, Guid complaintId, EVBSS.Api.Dtos.Complaints.CompleteReswapRequest request)
        {
            // Resolve SwapTransactionService lazily to avoid circular DI dependency
            var swapService = _serviceProvider?.GetService<SwapTransactionService>()
                ?? throw new InvalidOperationException("SwapTransactionService is not available via IServiceProvider.");

            // --- START: Auto-fill returned battery serial from the complaint's IssuedBattery when staff omits it ---
            var complaint = await _context.BatteryComplaints
                .Include(c => c.IssuedBattery)
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại (Complaint) để hoàn tất Re-swap.");

            var returnedBatterySerial = request?.ReturnedBatterySerial;

            if (string.IsNullOrWhiteSpace(returnedBatterySerial))
            {
                if (complaint.IssuedBattery == null || string.IsNullOrWhiteSpace(complaint.IssuedBattery.Serial))
                {
                    throw new InvalidOperationException("Không thể xác định Serial pin lỗi từ khiếu nại. Dữ liệu pin bị thiếu.");
                }

                returnedBatterySerial = complaint.IssuedBattery.Serial;
                _logger.LogInformation("Auto-filled ReturnedBatterySerial for complaint {ComplaintId} with known faulty battery serial: {Serial}", complaintId, returnedBatterySerial);
            }
            // --- END: Auto-fill logic ---

            // Find the reservation linked to this complaint
            var reservation = await _context.Reservations
                .Include(r => r.User).ThenInclude(u => u.Vehicles)
                .Include(r => r.Station)
                .Include(r => r.BatteryUnit)
                .FirstOrDefaultAsync(r => r.RelatedComplaintId == complaintId);

            if (reservation == null)
                throw new KeyNotFoundException("Không tìm thấy lịch hẹn (Reservation) cho khiếu nại này.");

            if (reservation.Status != ReservationStatus.CheckedIn)
                throw new InvalidOperationException($"Lịch hẹn Re-swap (ID: {reservation.Id}) phải ở trạng thái CheckedIn để hoàn tất.");

            // Build finalize request and reuse SwapTransactionService logic
            var finalizeRequest = new EVBSS.Api.Dtos.SwapTransactions.FinalizeSwapRequest
            {
                ReservationId = reservation.Id,
                // Use the determined/auto-filled serial
                OldBatterySerial = returnedBatterySerial,
                OldBatteryHealth = request?.ReturnedBatteryHealth
            };

            var swap = await swapService.FinalizeFromReservationAsync(finalizeRequest, staffId);

            // If the swap is related to a complaint, ensure complaint is finalized (FinalizeFromReservationAsync already does auto-finalize,
            // but call FinalizeComplaintAsync to be explicit if needed)
            try
            {
                if (swap.RelatedComplaintId.HasValue)
                {
                    var related = swap.RelatedComplaintId.Value;
                    // Use the complaint we loaded above
                    if (complaint != null && complaint.Status == ComplaintStatus.Confirmed)
                    {
                        await FinalizeComplaintAsync(staffId, related);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-finalize after CompleteReswapTransactionAsync failed for swap {SwapId}", swap.Id);
            }

            return swap;
        }

        // Generate a signed QR code payload: Base64( JSON(payload) + '|' + HMAC )
        private string GenerateQRCode(Guid reservationId)
        {
            var payload = new
            {
                rid = reservationId.ToString(),
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(payload);
            var signature = ComputeHMAC(json);

            var combined = $"{json}|{signature}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
        }

        private string ComputeHMAC(string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(QRSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Gộp 2 bước: Thu hồi pin lỗi VÀ Hoàn tất giao dịch đổi pin miễn phí (Re-swap) thành một bước duy nhất cho Staff.
        /// </summary>
        /// <param name="staffId">ID của Staff thực hiện.</param>
        /// <param name="complaintId">ID của khiếu nại.</param>
        /// <param name="staffStationId">ID trạm Staff đang xử lý (nơi pin lỗi được thu hồi).</param>
        /// <param name="request">DTO chứa Serial và Health của pin lỗi được thu hồi.</param>
        /// <returns>SwapTransaction mới đã hoàn tất.</returns>
        public async Task<SwapTransaction> ProcessAndCompleteReswapAsync(Guid staffId, Guid complaintId, Guid staffStationId, EVBSS.Api.Dtos.Complaints.CompleteReswapRequest request)
        {
            // Bước 1: Thu hồi pin lỗi (isChained = true để không gửi notification trung gian)
            await ProcessFaultyBatteryReturnAsync(staffId, complaintId, staffStationId, isChained: true);

            // Bước 2: Hoàn tất giao dịch cấp pin mới
            var swapTransaction = await CompleteReswapTransactionAsync(staffId, complaintId, request);

            return swapTransaction;
        }

        /// <summary>
        /// Lấy lịch hẹn đổi pin miễn phí (Re-swap Reservation) liên kết với một khiếu nại.
        /// </summary>
        /// <param name="complaintId">ID của khiếu nại.</param>
        /// <returns>Đối tượng Reservation hoặc null nếu không tìm thấy.</returns>
        public async Task<Reservation?> GetReservationByComplaintIdAsync(Guid complaintId)
        {
            return await _context.Reservations
                .FirstOrDefaultAsync(r => r.RelatedComplaintId == complaintId);
        }

        // Driver-facing: Create a free re-swap reservation linked to a confirmed complaint.
        public async Task<Reservation> DriverCreateReswapReservationAsync(Guid driverId, CreateReswapReservationRequest request)
        {
            var complaint = await _context.BatteryComplaints
                .Include(c => c.IssuedBattery)
                .FirstOrDefaultAsync(c => c.Id == request.ComplaintId && c.ReportedByUserId == driverId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại hoặc khiếu nại không thuộc về bạn.");

            if (complaint.Status != ComplaintStatus.Confirmed)
                throw new InvalidOperationException($"Chỉ có thể đặt lịch đổi pin khi khiếu nại ở trạng thái Confirmed. Trạng thái hiện tại: {complaint.Status}.");

            var existingReservation = await _context.Reservations
                .AnyAsync(r => r.RelatedComplaintId == request.ComplaintId);

            if (existingReservation)
                throw new InvalidOperationException("Đã có lịch hẹn đổi pin miễn phí cho khiếu nại này.");

            var newReservationId = Guid.NewGuid();
            var qrCodeBase64 = GenerateQRCode(newReservationId);

            var reservation = new Reservation
            {
                Id = newReservationId,
                UserId = driverId,
                StationId = request.StationId,
                // Ensure the issued battery and its model id are present. Do not silently use Guid.Empty.
                BatteryModelId = complaint.IssuedBattery?.BatteryModelId ?? throw new InvalidOperationException("Không thể xác định loại pin để đặt lịch hẹn đổi pin thay thế. Vui lòng liên hệ Staff."),
                SlotDate = DateOnly.FromDateTime(request.SlotDateTime),
                SlotStartTime = request.SlotDateTime.TimeOfDay,
                SlotEndTime = request.SlotDateTime.TimeOfDay.Add(TimeSpan.FromMinutes(30)),
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                RelatedComplaintId = complaint.Id,
                QRCode = qrCodeBase64,
                PaymentId = null,
                UserSubscriptionId = null
            };

            _context.Reservations.Add(reservation);

            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty)
                + $"\nDriver đã đặt lịch đổi pin miễn phí (Reservation ID: {reservation.Id}) tại trạm {request.StationId} vào lúc {request.SlotDateTime}.";

            await _context.SaveChangesAsync();
            return reservation;
        }

        /// <summary>
        /// Staff/Hệ thống tự động đóng khiếu nại sau khi quá trình Re-swap hoàn tất.
        /// </summary>
        public async Task<BatteryComplaint> FinalizeComplaintAsync(Guid staffId, Guid complaintId)
        {
            var complaint = await _context.BatteryComplaints
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

            // Chỉ có thể Resolved khi khiếu nại đã được xác nhận (Confirmed)
            if (complaint.Status != ComplaintStatus.Confirmed)
                throw new InvalidOperationException($"Chỉ có thể giải quyết (Resolve) khiếu nại đã được xác nhận (Confirmed). Trạng thái hiện tại: {complaint.Status}.");

            complaint.Status = ComplaintStatus.Resolved;
            complaint.HandledByStaffId = staffId; // Hoặc dùng StaffId đang thực hiện Re-swap
            complaint.ResolvedAt = DateTime.UtcNow;
            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty) + "\n[System Note]: Complaint finalized after successful battery re-swap.";

            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = complaint.ReportedByUserId,
                SenderId = staffId,
                Message = $"Khiếu nại số {complaint.Id} đã được ĐÓNG (Resolved) sau khi bạn hoàn tất giao dịch đổi pin thay thế. Cảm ơn bạn.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                Type = NotificationType.Generic,
                RelatedEntityId = complaint.Id
            });

            await _context.SaveChangesAsync();

            return complaint;
        }
    }
}