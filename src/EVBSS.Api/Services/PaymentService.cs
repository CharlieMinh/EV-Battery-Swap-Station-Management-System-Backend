using EVBSS.Api.Configuration;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EVBSS.Api.Services;

public interface IPaymentService
{
    Task<SelectCashMethodResponse> SelectCashMethodAsync(Guid userId, Guid paymentId);

    Task<Payment> CompleteCashPaymentAsync(Guid paymentId, Guid staffId);

    Task<CreatePayPerSwapReservationResponse> CreatePayPerSwapReservationAsync(Guid userId, CreatePayPerSwapReservationRequest request, string ipAddress);

    /// <summary>
    /// Lấy danh sách payments (cho Staff/Admin dashboard hoặc Driver xem payment của mình)
    /// </summary>
    Task<(List<PaymentListResponse> Payments, int TotalCount)> GetPaymentsAsync(
        int page,
        int pageSize,
        PaymentStatus? status = null,
        PaymentMethod? method = null,
        PaymentType? type = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? userId = null);

    /// <summary>
    /// Lấy chi tiết 1 payment
    /// </summary>
    Task<PaymentDetailResponse?> GetPaymentDetailAsync(Guid paymentId);
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly SlotReservationService _slotReservationService;
    private readonly VnPayConfig _vnPayConfig;

    public PaymentService(
        AppDbContext context,
        ILogger<PaymentService> logger,
        SlotReservationService slotReservationService,
        IOptions<VnPayConfig> vnPayConfig)
    {
        _context = context;
        _logger = logger;
        _slotReservationService = slotReservationService;
        _vnPayConfig = vnPayConfig.Value;
    }

    public async Task<SelectCashMethodResponse> SelectCashMethodAsync(Guid userId, Guid paymentId)
    {
        try
        {
            var payment = await _context.Payments
                .Include(p => p.UserSubscription)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return new SelectCashMethodResponse { Success = false, Message = "Không tìm thấy payment." };
            }

            if (payment.UserId != userId)
            {
                return new SelectCashMethodResponse { Success = false, Message = "Payment này không thuộc về bạn." };
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                return new SelectCashMethodResponse { Success = false, Message = $"Payment đã được xử lý ({payment.Status}). Không thể thay đổi phương thức thanh toán." };
            }

            payment.Method = PaymentMethod.Cash;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment {PaymentId} switched to CASH method by user {UserId}", paymentId, userId);

            return new SelectCashMethodResponse
            {
                Success = true,
                Message = "Đã chuyển sang phương thức thanh toán tiền mặt.",
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Instructions = "Vui lòng đến bất kỳ trạm đổi pin nào để thanh toán tiền mặt và kích hoạt gói. Xuất trình mã thanh toán cho nhân viên trạm."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting cash method for payment {PaymentId}", paymentId);
            return new SelectCashMethodResponse { Success = false, Message = "Có lỗi xảy ra khi chuyển phương thức thanh toán." };
        }
    }

    public async Task<Payment> CompleteCashPaymentAsync(Guid paymentId, Guid staffId)
    {
        var payment = await _context.Payments
            .Include(p => p.UserSubscription) // Include the related UserSubscription
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thanh toán.");
        }

        if (payment.Method != PaymentMethod.Cash)
        {
            throw new InvalidOperationException("Đây không phải là thanh toán bằng tiền mặt.");
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"Thanh toán đã ở trạng thái {payment.Status}, không thể xác nhận.");
        }

        // --- Start of new logic ---
        // Check if this payment is for a subscription and activate it
        if (payment.Type == PaymentType.Subscription && payment.UserSubscriptionId.HasValue && payment.UserSubscription != null)
        {
            var userSubscription = payment.UserSubscription;
            var now = DateTime.UtcNow;

            // Activate the subscription and set its dates (đồng bộ với VnPayService)
            userSubscription.IsActive = true;
            userSubscription.StartDate = now;
            userSubscription.EndDate = now.AddDays(30);  // ⭐ THÊM EndDate để đồng bộ
            userSubscription.CurrentBillingPeriodStart = now;
            userSubscription.CurrentBillingPeriodEnd = now.AddDays(30);
            userSubscription.LastPaymentDate = now;
            userSubscription.UpdatedAt = now;

            _logger.LogInformation(
                "Subscription {UserSubscriptionId} ACTIVATED for user {UserId} upon cash payment. Valid from {Start} to {End}",
                userSubscription.Id,
                userSubscription.UserId,
                now.ToString("yyyy-MM-dd HH:mm:ss"),
                now.AddDays(30).ToString("yyyy-MM-dd HH:mm:ss")
            );
        }
        // --- End of new logic ---

        // Update payment status
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = DateTime.UtcNow;
        payment.ProcessedByStaffId = staffId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cash payment {PaymentId} was completed by staff {StaffId}.",
            payment.Id, staffId);

        return payment;
    }

    public async Task<CreatePayPerSwapReservationResponse> CreatePayPerSwapReservationAsync(
        Guid userId,
        CreatePayPerSwapReservationRequest request,
        string ipAddress)
    {
        // Bắt đầu transaction với 'using' để đảm bảo tự động dispose/rollback khi có lỗi
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            Reservation reservation;
            try
            {
                // Cố gắng tạo reservation
                reservation = await _slotReservationService.CreateReservationAsync(
                    userId,
                    request.StationId,
                    request.VehicleId,
                    request.SlotDate,
                    request.SlotStartTime,
                    request.SlotEndTime,
                    request.PaymentMethod);
            }
            // Xử lý các exception cụ thể đã biết
            catch (ActiveReservationExistsException ex)
            {
                _logger.LogWarning(ex, "User {UserId} already has an active reservation.", userId);
                // Không cần rollback tường minh, 'using' sẽ xử lý.
                return new CreatePayPerSwapReservationResponse { Success = false, Message = ex.Message };
            }
            catch (SlotNotAvailableException ex)
            {
                _logger.LogWarning(ex, "Slot not available for user {UserId}.", userId);
                // Không cần rollback tường minh, 'using' sẽ xử lý.
                return new CreatePayPerSwapReservationResponse { Success = false, Message = ex.Message };
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for user {UserId}: {Message}", userId, ex.Message);
                // Không cần rollback tường minh, 'using' sẽ xử lý.
                return new CreatePayPerSwapReservationResponse { Success = false, Message = ex.Message };
            }

            // Tạo Payment
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReservationId = reservation.Id, // Đảm bảo reservation không null ở đây
                UserSubscriptionId = null,
                Method = request.PaymentMethod,
                Type = PaymentType.PayPerSwap,
                Amount = request.Amount,
                Status = PaymentStatus.Pending,
                Description = $"Thanh toán đặt lịch đổi pin - {request.SlotDate:dd/MM/yyyy} {request.SlotStartTime:hh\\:mm}-{request.SlotEndTime:hh\\:mm}", // Giữ lại format đúng
                VnpTxnRef = GenerateTransactionReference(),
                PaymentReference = GenerateTransactionReference(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            // Lưu các thay đổi (Reservation và Payment) trong transaction
            await _context.SaveChangesAsync(); // Có thể ném ra DbUpdateException,...

            // Commit transaction dựa trên phương thức thanh toán *CHỈ KHI* mọi thứ thành công đến đây
            if (request.PaymentMethod == PaymentMethod.VNPay)
            {
                var paymentUrl = GenerateVnPayUrlForReservation(payment, reservation, ipAddress); // Có thể ném ra lỗi format,...
                await transaction.CommitAsync(); // Commit tường minh CHỈ trên đường dẫn thành công
                return new CreatePayPerSwapReservationResponse
                {
                    Success = true,
                    Message = "Đã tạo lịch hẹn thành công. Vui lòng thanh toán qua VNPay.",
                    PaymentUrl = paymentUrl,
                    ReservationId = reservation.Id,
                    PaymentId = payment.Id,
                    Amount = payment.Amount,
                    Status = "Pending"
                };
            }
            else // Cash
            {
                await transaction.CommitAsync(); // Commit tường minh CHỈ trên đường dẫn thành công
                                                 // Kiểm tra lại format thời gian trong Instructions nếu cần
                string instructions = $"Vui lòng đến trạm đúng giờ hẹn ({reservation.SlotDate:dd/MM/yyyy} {reservation.SlotStartTime:hh\\:mm}-{reservation.SlotEndTime:hh\\:mm}) và xuất trình mã QR này để check-in. Thanh toán {payment.Amount:N0} VNĐ bằng tiền mặt tại quầy.";
                return new CreatePayPerSwapReservationResponse
                {
                    Success = true,
                    Message = "Đã tạo lịch hẹn thành công. Vui lòng thanh toán tiền mặt tại trạm khi check-in.",
                    ReservationId = reservation.Id,
                    PaymentId = payment.Id,
                    QRCode = reservation.QRCode, // Đảm bảo QRCode được gán đúng
                    Status = "Pending",
                    Amount = payment.Amount,
                    Instructions = instructions
                };
            }
        }
        catch (Exception ex) // Bắt TẤT CẢ exception từ khối try chính
        {
            // ----- ĐÃ XÓA ROLLBACK TƯỜNG MINH -----
            // await transaction.RollbackAsync(); // <<<< DÒNG NÀY ĐÃ BỊ XÓA

            // Log lỗi
            _logger.LogError(ex, "Error creating pay-per-swap reservation for user {UserId}", userId);

            // Trả về lỗi chung
            // Khối 'using' sẽ tự động gọi Dispose() trên transaction,
            // và sẽ tự động rollback vì CommitAsync() chưa được gọi thành công.
            return new CreatePayPerSwapReservationResponse { Success = false, Message = "Có lỗi xảy ra khi tạo lịch đặt. Vui lòng thử lại." };
        }
        // Code không nên đến được đây vì các nhánh đều có return.
        // Nếu đến được, 'using' cũng sẽ đảm bảo rollback.
    }

    private string GenerateTransactionReference()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(100, 999);
        return $"EVB{timestamp}{random}";
    }

    private string GenerateVnPayUrlForReservation(Payment payment, Reservation reservation, string ipAddress)
    {
        var orderInfo = $"Thanh toán đặt lịch đổi pin - {reservation.SlotDate:dd/MM/yyyy} {reservation.SlotStartTime:hh:mm}";

        var vnpParams = new Dictionary<string, string>
        {
            {"vnp_Version", "2.1.0"},
            {"vnp_Command", "pay"},
            {"vnp_TmnCode", _vnPayConfig.TmnCode},
            {"vnp_Amount", ((long)(payment.Amount * 100)).ToString()},
            {"vnp_CurrCode", "VND"},
            {"vnp_TxnRef", payment.VnpTxnRef!},
            {"vnp_OrderInfo", orderInfo},
            {"vnp_OrderType", "other"},
            {"vnp_Locale", "vn"},
            {"vnp_ReturnUrl", _vnPayConfig.ReturnUrl},
            {"vnp_IpnUrl", _vnPayConfig.IpnUrl},
            {"vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")},
            {"vnp_IpAddr", ipAddress}
        };

        var sortedParams = vnpParams.OrderBy(x => x.Key).ToList();
        var hashData = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
        var vnpSecureHash = ComputeHmacSha512(_vnPayConfig.HashSecret, hashData);

        var queryString = string.Join("&", sortedParams.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));

        return $"{_vnPayConfig.BaseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    private string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hashBytes).ToLower();
    }

    /// <summary>
    /// Lấy danh sách payments với filtering và pagination (cho Staff/Admin)
    /// </summary>
    public async Task<(List<PaymentListResponse> Payments, int TotalCount)> GetPaymentsAsync(
        int page,
        int pageSize,
        PaymentStatus? status = null,
        PaymentMethod? method = null,
        PaymentType? type = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? userId = null)
    {
        var query = _context.Payments
            .Include(p => p.User)
            .Include(p => p.UserSubscription)
                .ThenInclude(us => us!.SubscriptionPlan)
            .Include(p => p.Reservation)
            .AsQueryable();

        // ⭐ Filter by userId if provided (for Driver to see only their payments)
        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);

        // Apply filters
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (method.HasValue)
            query = query.Where(p => p.Method == method.Value);

        if (type.HasValue)
            query = query.Where(p => p.Type == type.Value);

        if (fromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(p => p.CreatedAt <= toDate.Value);

        // Get total count
        var totalCount = await query.CountAsync();

        // Apply pagination and get results
        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentListResponse
            {
                Id = p.Id,
                UserId = p.UserId,
                UserName = p.User.Name,
                UserEmail = p.User.Email,
                UserPhone = p.User.Phone,
                UserSubscriptionId = p.UserSubscriptionId,
                SubscriptionPlanName = p.UserSubscription != null ? p.UserSubscription.SubscriptionPlan.Name : null,
                ReservationId = p.ReservationId,
                Method = p.Method.ToString(),
                Type = p.Type.ToString(),
                Amount = p.Amount,
                Status = p.Status.ToString(),
                Description = p.Description,
                VnpTxnRef = p.VnpTxnRef,
                PaymentReference = p.PaymentReference,
                VnpResponseCode = p.VnpResponseCode,
                VnpTransactionNo = p.VnpTransactionNo,
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt,
                ProcessedByStaffId = p.ProcessedByStaffId,
                ProcessedByStaffName = p.ProcessedByStaffId.HasValue
                    ? _context.Users.Where(u => u.Id == p.ProcessedByStaffId).Select(u => u.Name).FirstOrDefault()
                    : null
            })
            .ToListAsync();

        return (payments, totalCount);
    }

    /// <summary>
    /// Lấy chi tiết 1 payment (cho Staff/Admin)
    /// </summary>
    public async Task<PaymentDetailResponse?> GetPaymentDetailAsync(Guid paymentId)
    {
        var payment = await _context.Payments
            .Include(p => p.User)
            .Include(p => p.UserSubscription)
                .ThenInclude(us => us!.SubscriptionPlan)
            .Include(p => p.Reservation)
                .ThenInclude(r => r!.Station)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            return null;

        var detail = new PaymentDetailResponse
        {
            Id = payment.Id,
            UserId = payment.UserId,
            UserName = payment.User.Name,
            UserEmail = payment.User.Email,
            UserPhone = payment.User.Phone,
            UserSubscriptionId = payment.UserSubscriptionId,
            SubscriptionPlanName = payment.UserSubscription?.SubscriptionPlan.Name,
            ReservationId = payment.ReservationId,
            Method = payment.Method.ToString(),
            Type = payment.Type.ToString(),
            Amount = payment.Amount,
            Status = payment.Status.ToString(),
            Description = payment.Description,
            VnpTxnRef = payment.VnpTxnRef,
            PaymentReference = payment.PaymentReference,
            VnpResponseCode = payment.VnpResponseCode,
            VnpTransactionNo = payment.VnpTransactionNo,
            CreatedAt = payment.CreatedAt,
            CompletedAt = payment.CompletedAt,
            ProcessedByStaffId = payment.ProcessedByStaffId,
            ProcessedByStaffName = payment.ProcessedByStaffId.HasValue
                ? await _context.Users.Where(u => u.Id == payment.ProcessedByStaffId).Select(u => u.Name).FirstOrDefaultAsync()
                : null,

            // User info
            User = new PaymentUserInfo
            {
                Id = payment.User.Id,
                FullName = payment.User.Name ?? "N/A",
                Email = payment.User.Email,
                PhoneNumber = payment.User.Phone
            },

            // Subscription info (if applicable)
            Subscription = payment.UserSubscription != null ? new PaymentSubscriptionInfo
            {
                UserSubscriptionId = payment.UserSubscription.Id,
                PlanName = payment.UserSubscription.SubscriptionPlan.Name,
                Price = payment.UserSubscription.SubscriptionPlan.MonthlyPrice,
                MaxSwapsPerMonth = payment.UserSubscription.SubscriptionPlan.MaxSwapsPerMonth,
                StartDate = payment.UserSubscription.StartDate,
                EndDate = payment.UserSubscription.EndDate
            } : null,

            // Reservation info (if applicable)
            Reservation = payment.Reservation != null ? new PaymentReservationInfo
            {
                ReservationId = payment.Reservation.Id,
                StationName = payment.Reservation.Station.Name,
                SlotDate = payment.Reservation.SlotDate,
                SlotStartTime = payment.Reservation.SlotStartTime,
                SlotEndTime = payment.Reservation.SlotEndTime,
                Status = payment.Reservation.Status.ToString()
            } : null
        };

        return detail;
    }
}