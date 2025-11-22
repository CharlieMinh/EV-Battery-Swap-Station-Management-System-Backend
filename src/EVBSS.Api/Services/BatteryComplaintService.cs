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
using EVBSS.Api.Dtos.SwapTransactions; // Cần thêm namespace này
using EVBSS.Api.Dtos.Reservations; // For CreateReservationRequest-derived DTOs

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

            // FIX: Chỉ cho phép chuyển từ CheckedIn (pin đã ở trạm) sang Investigating.
            if (complaint.Status != ComplaintStatus.CheckedIn)
                throw new InvalidOperationException($"Chỉ có thể chuyển sang Investigating từ trạng thái CheckedIn. Trạng thái hiện tại: {complaint.Status}.");

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

        /// <summary>
        /// Transition a complaint into the CheckedIn state when a linked reservation is checked-in by staff.
        /// Idempotent: if already CheckedIn, returns the complaint unchanged.
        /// Allowed transition: Scheduled -> CheckedIn (Chỉ áp dụng cho lịch kiểm tra ban đầu)
        /// </summary>
        public async Task<BatteryComplaint> TransitionToCheckedInAsync(Guid complaintId, Guid staffId)
        {
            var complaint = await _context.BatteryComplaints
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

            // If already CheckedIn, return early (idempotent)
            if (complaint.Status == ComplaintStatus.CheckedIn)
                return complaint;


            // FIX: Chỉ cho phép chuyển từ Scheduled sang CheckedIn (loại bỏ AwaitingReswap và PendingScheduling)
            if (complaint.Status != ComplaintStatus.Scheduled)
            {
                throw new InvalidOperationException($"Không thể chuyển khiếu nại sang CheckedIn từ trạng thái hiện tại: {complaint.Status}. Chỉ chấp nhận Scheduled.");
            }

            complaint.Status = ComplaintStatus.CheckedIn;
            complaint.HandledByStaffId = staffId;
            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty)
                + $"\n[System Note]: Complaint transitioned to CheckedIn by Staff {staffId} at {DateTime.UtcNow}.";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Complaint {ComplaintId} transitioned to CheckedIn by staff {StaffId}", complaintId, staffId);

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
                Status = ComplaintStatus.PendingScheduling,
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

            // Enforce stricter flow: staff must investigate (Investigating) before making a decision
            if (complaint.Status != ComplaintStatus.Investigating)
                throw new InvalidOperationException($"Chỉ có thể ra quyết định khi khiếu nại đang ở trạng thái Investigating. Trạng thái hiện tại: {complaint.Status}.");

            complaint.Status = request.NewStatus;
            complaint.HandledByStaffId = staffId;
            // Append resolution notes instead of overwriting to preserve history
            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty)
                + $"\n[Decision Note - {request.NewStatus}]: " + (request.ResolutionNotes ?? string.Empty);

            string message;
            
            // ==========================================================================================
            // ⭐ FIX: Xử lý Reservation khi Complaint thay đổi trạng thái
            // ==========================================================================================
            
            // Tìm lịch hẹn kiểm tra (Inspection Reservation) đang active liên quan đến khiếu nại này
            var inspectionReservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.RelatedComplaintId == complaintId 
                                       && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn));

            if (request.NewStatus == ComplaintStatus.Confirmed)
            {
                message = $"Khiếu nại pin lỗi đã được XÁC NHẬN (Confirmed). Staff sẽ tiến hành đổi pin thay thế cho bạn.";
                // Lưu ý: Nếu Confirmed, Reservation vẫn giữ nguyên (thường là CheckedIn) 
                // để Staff tiếp tục dùng nó cho việc Finalize Reswap.
            }
            else // Rejected
            {
                message = $"Khiếu nại pin lỗi đã bị TỪ CHỐI. Pin không phát hiện lỗi hệ thống. Chi tiết: {request.ResolutionNotes}";

                // ⭐ FIX: Nếu từ chối, ta phải "thả" Reservation ra để User không bị kẹt
                // Chuyển trạng thái Reservation sang Completed (vì quy trình kiểm tra đã xong)
                if (inspectionReservation != null)
                {
                    inspectionReservation.Status = ReservationStatus.Completed;
                    // Ghi chú thêm vào lý do hủy/hoàn thành nếu cần
                    inspectionReservation.CancelNote = "Inspection completed: Complaint Rejected.";
                    
                    _logger.LogInformation("Auto-completed inspection reservation {ReservationId} because complaint {ComplaintId} was rejected.", 
                        inspectionReservation.Id, complaintId);
                }
            }
            // ==========================================================================================

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

        // Backwards-compat wrappers for controllers / services that still call legacy methods.
        // These are thin shims that call the consolidated immediate-reswap flow where appropriate.
        public async Task<SwapTransaction> ProcessAndCompleteReswapAsync(Guid staffId, Guid complaintId, Guid staffStationId, CompleteReswapRequest request)
        {
            // Backwards-compat: finalize a confirmed complaint. This wrapper will call the new finalize method.
            var swap = await FinalizeConfirmedReswapAsync(staffId, complaintId, staffStationId, request);
            return swap;
        }

        // Legacy reservation-based "DriverCreateReswapReservationAsync" has been removed.
        // Use DriverScheduleInitialInspectionAsync to create the single inspection reservation
        // that covers the lifecycle from PendingScheduling -> Scheduled and then use
        // staff workflows (Investigate -> Resolve -> FinalizeConfirmedReswapAsync) to
        // complete re-swap processing.

        public async Task<BatteryComplaint> FinalizeComplaintAsync(Guid staffId, Guid complaintId)
        {
            var complaint = await _context.BatteryComplaints
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

            // Force-close the complaint as Resolved (used by SwapTransactionService auto-finalize)
            if (complaint.Status != ComplaintStatus.Resolved)
            {
                complaint.Status = ComplaintStatus.Resolved;
                complaint.HandledByStaffId = staffId;
                complaint.ResolvedAt = DateTime.UtcNow;
                complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty) + "\n[System Note]: Complaint finalized after related re-swap.";

                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = complaint.ReportedByUserId,
                    SenderId = staffId,
                    Message = $"Khiếu nại số {complaint.Id} đã được ĐÓNG (Resolved) sau khi giao dịch đổi pin liên quan hoàn tất.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    Type = NotificationType.Generic,
                    RelatedEntityId = complaint.Id
                });

                await _context.SaveChangesAsync();
            }

            return complaint;
        }

        // NOTE: Legacy/reservation-driven re-swap flows removed. If you need reservation-based flows,
        // reintroduce them carefully and ensure ComplaintStatus enum mappings align with DB migrations.

        /// <summary>
        /// Staff thực hiện thu hồi pin lỗi và hoàn tất giao dịch đổi pin mới (Re-swap).
        /// Chỉ được gọi sau khi Complaint đã được Staff Confirm (Status = Confirmed).
        /// </summary>
        public async Task<SwapTransaction> FinalizeConfirmedReswapAsync(
            Guid staffId,
            Guid complaintId,
            Guid staffStationId,
            CompleteReswapRequest request)
        {
            // Resolve SwapTransactionService lazily
            var swapService = _serviceProvider?.GetService<SwapTransactionService>()
                ?? throw new InvalidOperationException("SwapTransactionService is not available via IServiceProvider.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var complaint = await GetComplaintByIdAsync(complaintId);

                // --- 1. KIỂM TRA TRẠNG THÁI ---
                // Yêu cầu Complaint phải ở trạng thái Confirmed (Chờ finalize)
                if (complaint.Status != ComplaintStatus.Confirmed)
                {
                    throw new InvalidOperationException($"Chỉ có thể hoàn tất đổi pin khi khiếu nại ở trạng thái Confirmed. Trạng thái hiện tại: {complaint.Status}.");
                }

                // --- 2. TÌM RESERVATION BAN ĐẦU ---
                var reservation = await _context.Reservations
                    .Include(r => r.User).ThenInclude(u => u.Vehicles)
                    .Include(r => r.Station)
                    .Include(r => r.BatteryUnit)
                    // BUG FIX: require the reservation to be explicitly linked to this complaint
                    .Where(r => r.RelatedComplaintId == complaint.Id
                             && r.UserId == complaint.ReportedByUserId 
                             && r.Status == ReservationStatus.CheckedIn)
                    .OrderByDescending(r => r.SlotDate)
                    .ThenByDescending(r => r.SlotStartTime)
                    .FirstOrDefaultAsync();

                if (reservation == null)
                    throw new InvalidOperationException("Không tìm thấy lịch hẹn (Reservation) đang ở trạng thái CheckedIn để hoàn tất giao dịch. Driver phải Check-in trước.");
                    
                // Liên kết Reservation ban đầu với Complaint để audit
                if (reservation.RelatedComplaintId == null)
                {
                    reservation.RelatedComplaintId = complaint.Id;
                }
                
                // --- 3. THU HỒI PIN LỖI (VÀ CẬP NHẬT GHI CHÚ COMPLAINT) ---
                var issuedBattery = complaint.IssuedBattery
                    ?? throw new InvalidOperationException("Không tìm thấy thông tin pin bị lỗi.");

                // NOTE: Inventory/status updates for the returned (old) battery are now handled
                // centrally by SwapTransactionService.FinalizeFromReservationAsync so we avoid
                // performing inventory changes here and rely on the outer transaction to commit
                // everything atomically. We only record metadata (SOH) if provided and audit notes.
                if (request.ReturnedBatteryHealth.HasValue)
                {
                    // If BatteryUnit contains a SOH-like property in the future, set it here.
                    // For now we append the reported health to resolution notes for audit.
                    complaint.ResolutionNotes += $"\nReported SOH: {request.ReturnedBatteryHealth.Value}%.";
                }

                complaint.HandledByStaffId = staffId;
                complaint.ResolutionNotes += $"\nPin lỗi đã được thu hồi bởi Staff {staffId} tại trạm {staffStationId}.";
                
                // --- 4. HOÀN TẤT CẤP PIN MỚI (Finalize Swap) ---
                // Delegate returned-battery detection/creation and inventory changes to
                // SwapTransactionService.FinalizeFromReservationAsync. We only pass the
                // reservation id and optional reported OldBatteryHealth.
                var finalizeRequest = new FinalizeSwapRequest
                {
                    ReservationId = reservation.Id,
                    OldBatteryHealth = request?.ReturnedBatteryHealth
                };

                var swapTransaction = await swapService.FinalizeFromReservationAsync(finalizeRequest, staffId);

                // --- 5. CHUYỂN TRẠNG THÁI SANG RESOLVED VÀ GỬI THÔNG BÁO ---
                if (swapTransaction.RelatedComplaintId.HasValue && swapTransaction.RelatedComplaintId.Value == complaint.Id)
                {
                    complaint.Status = ComplaintStatus.Resolved;
                    complaint.HandledByStaffId = staffId;
                    complaint.ResolvedAt = DateTime.UtcNow;
                    complaint.ResolutionNotes += "\n[System Note]: Complaint finalized after successful battery re-swap.";

                    _context.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = complaint.ReportedByUserId,
                        SenderId = staffId,
                        Message = $"Khiếu nại số {complaint.Id} đã được ĐÓNG (Resolved) và giao dịch đổi pin thay thế đã hoàn tất. Cảm ơn bạn.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        Type = NotificationType.Generic,
                        RelatedEntityId = complaint.Id
                    });
                }
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully finalized Reswap for complaint {ComplaintId} by staff {StaffId}", complaintId, staffId);

                return swapTransaction;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error finalizing Reswap for complaint {ComplaintId}", complaintId);
                throw;
            }
        }

        /// <summary>
        /// Driver đặt lịch hẹn kiểm tra ban đầu (liên kết với Complaint) sau khi báo lỗi.
        /// Tạo reservation thông qua SlotReservationService và cập nhật Complaint.Status -> Scheduled.
        /// </summary>
        public async Task<Reservation> DriverScheduleInitialInspectionAsync(Guid driverId, CreateInspectionReservationRequest request)
        {
            // Resolve SlotReservationService lazily to avoid DI cycle
            var slotReservationService = _serviceProvider?.GetService<SlotReservationService>()
                ?? throw new InvalidOperationException("SlotReservationService is not available via IServiceProvider.");

            var complaint = await _context.BatteryComplaints
                .Include(c => c.SwapTransaction) // <--- Eager load SwapTransaction
                .FirstOrDefaultAsync(c => c.Id == request.ComplaintId && c.ReportedByUserId == driverId);

            if (complaint == null)
                throw new KeyNotFoundException("Không tìm thấy khiếu nại hoặc khiếu nại không thuộc về bạn.");

            if (complaint.Status != ComplaintStatus.PendingScheduling)
                throw new InvalidOperationException($"Chỉ có thể đặt lịch kiểm tra khi khiếu nại ở trạng thái PendingScheduling. Trạng thái hiện tại: {complaint.Status}.");
            
            // ⭐ Lấy VehicleId từ SwapTransaction
            if (complaint.SwapTransaction == null)
                throw new InvalidOperationException("Không tìm thấy giao dịch đổi pin liên quan đến khiếu nại này.");

            var vehicleId = complaint.SwapTransaction.VehicleId; // <<-- RESOLVE VEHICLE ID TỪ SWAP TRANSACTION

            var existingActiveReservation = await _context.Reservations
                .AnyAsync(r => r.RelatedComplaintId == request.ComplaintId
                            && r.Status != ReservationStatus.Cancelled
                            && r.Status != ReservationStatus.Expired
                            && r.Status != ReservationStatus.Completed);

            if (existingActiveReservation)
                throw new InvalidOperationException("Đã có lịch hẹn kiểm tra ban đầu đang hoạt động cho khiếu nại này. Vui lòng kiểm tra trạng thái lịch hẹn hiện tại.");

            // 1) Create reservation via SlotReservationService
            var reservation = await slotReservationService.CreateReservationAsync(
                userId: driverId,
                stationId: request.StationId,
                vehicleId: vehicleId, // <<-- PASS RESOLVED VEHICLE ID
                slotDate: request.SlotDate,
                slotStartTime: request.SlotStartTime,
                slotEndTime: request.SlotEndTime,
                paymentMethod: null,
                // Truyền ComplaintId để bỏ qua kiểm tra thanh toán
                relatedComplaintId: complaint.Id);

            // 2) Link reservation <-> complaint and update status
            complaint.Status = ComplaintStatus.Scheduled;

            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty)
                + $"\nDriver đã đặt lịch kiểm tra ban đầu (Reservation ID: {reservation.Id}) tại trạm {request.StationId} vào lúc {request.SlotDate} {request.SlotStartTime}.";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Scheduled initial inspection reservation {ReservationId} for complaint {ComplaintId}", reservation.Id, complaint.Id);

            return reservation;
        }
    }
}