using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.SwapTransactions;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

public class SwapTransactionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SwapTransactionService> _logger;
    private readonly IBatteryInventoryService _inventoryService;

    public SwapTransactionService(
        AppDbContext context, 
        ILogger<SwapTransactionService> logger,
        IBatteryInventoryService inventoryService)
    {
        _context = context;
        _logger = logger;
        _inventoryService = inventoryService;
    }

    public async Task<SwapTransaction> StartSwapAsync(Guid userId, StartSwapRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Validate vehicle belongs to user
            var vehicle = await _context.Vehicles
                .Include(v => v.CompatibleModel)
                .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.UserId == userId);

            if (vehicle == null)
                throw new InvalidOperationException("Vehicle not found or does not belong to user");

            // 2. Validate station exists and is active
            var station = await _context.Stations
                .FirstOrDefaultAsync(s => s.Id == request.StationId && s.IsActive);

            if (station == null)
                throw new InvalidOperationException("Station not found or not active");

            // 3. Check if station has compatible batteries available
            var availableBattery = await _context.BatteryUnits
                .FirstOrDefaultAsync(b => b.StationId == request.StationId && 
                                        b.BatteryModelId == vehicle.CompatibleBatteryModelId && 
                                        b.Status == BatteryStatus.Full);

            if (availableBattery == null)
                throw new InvalidOperationException($"No compatible batteries available at this station for {vehicle.CompatibleModel.Name}");

            // 4. If reservation provided, validate it
            Reservation? reservation = null;
            if (request.ReservationId.HasValue)
            {
                reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == request.ReservationId && r.UserId == userId);

                if (reservation == null)
                    throw new InvalidOperationException("Reservation not found or does not belong to user");

                if (reservation.Status != ReservationStatus.CheckedIn && reservation.Status != ReservationStatus.Pending)
                    throw new InvalidOperationException($"Reservation status is {reservation.Status}, cannot start swap");
            }

            // 5. Get user's active subscription (if any)
            var activeSubscription = await _context.UserSubscriptions
                .Include(us => us.SubscriptionPlan)
                .FirstOrDefaultAsync(us => us.UserId == userId && 
                                         us.IsActive == true && 
                                         us.StartDate <= DateTime.UtcNow && 
                                         (us.EndDate == null || us.EndDate > DateTime.UtcNow));

            // 6. Generate transaction number
            var transactionNumber = await GenerateTransactionNumberAsync();

            // 7. Create swap transaction
            var swapTransaction = new SwapTransaction
            {
                TransactionNumber = transactionNumber,
                UserId = userId,
                ReservationId = request.ReservationId,
                StationId = request.StationId,
                VehicleId = request.VehicleId,
                UserSubscriptionId = activeSubscription?.Id,
                PaymentType = activeSubscription != null ? PaymentType.Subscription : PaymentType.PayPerSwap,
                Status = SwapTransactionStatus.CheckedIn,
                StartedAt = DateTime.UtcNow,
                CheckedInAt = DateTime.UtcNow,
                Notes = request.Notes
            };

            // 8. Calculate fees
            await CalculateSwapFeesAsync(swapTransaction, activeSubscription);

            _context.SwapTransactions.Add(swapTransaction);

            // 9. Update reservation status if provided
            if (reservation != null)
            {
                reservation.Status = ReservationStatus.Completed;
            }

            await _context.SaveChangesAsync();

            // Load navigation properties for response
            await _context.Entry(swapTransaction)
                .Reference(s => s.Station)
                .LoadAsync();
            await _context.Entry(swapTransaction)
                .Reference(s => s.Vehicle)
                .LoadAsync();
            await _context.Entry(swapTransaction)
                .Reference(s => s.User)
                .LoadAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Swap transaction started: {TransactionNumber} for user {UserId} at station {StationId}", 
                transactionNumber, userId, request.StationId);

            return swapTransaction;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SwapTransaction> CompleteSwapAsync(Guid swapId, Guid userId, CompleteSwapRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Get swap transaction
            var swap = await _context.SwapTransactions
                .Include(s => s.IssuedBattery)
                .Include(s => s.Vehicle)
                .Include(s => s.Station)
                .Include(s => s.User)
                .Include(s => s.UserSubscription)
                .FirstOrDefaultAsync(s => s.Id == swapId && s.UserId == userId);

            if (swap == null)
                throw new InvalidOperationException("Swap transaction not found or does not belong to user");

            if (swap.Status != SwapTransactionStatus.CheckedIn && swap.Status != SwapTransactionStatus.BatteryReturned)
                throw new InvalidOperationException($"Swap status is {swap.Status}, cannot complete");

            // 2. Check subscription swap limit BEFORE completing (if user has subscription)
            if (swap.UserSubscriptionId.HasValue)
            {
                var subscription = await _context.UserSubscriptions
                    .Include(us => us.SubscriptionPlan)
                    .FirstOrDefaultAsync(us => us.Id == swap.UserSubscriptionId);

                if (subscription != null && subscription.SubscriptionPlan.MaxSwapsPerMonth.HasValue)
                {
                    // Kiểm tra ĐÃ ĐẠT giới hạn chưa (TRƯỚC khi tăng counter)
                    if (subscription.CurrentMonthSwapCount >= subscription.SubscriptionPlan.MaxSwapsPerMonth.Value)
                    {
                        throw new InvalidOperationException(
                            $"Đã đạt giới hạn {subscription.SubscriptionPlan.MaxSwapsPerMonth} lần đổi pin trong tháng này. " +
                            $"Hiện tại: {subscription.CurrentMonthSwapCount}/{subscription.SubscriptionPlan.MaxSwapsPerMonth} lần. " +
                            $"Vui lòng nâng cấp gói hoặc chờ đến chu kỳ thanh toán tiếp theo.");
                    }
                }
            }

            // Pin trả về là pin cũ của khách hàng (không có trong database trạm)
            // Chỉ cần lưu thông tin serial và sức khỏe pin
            
            // 3. Update swap transaction
            if (swap.Status == SwapTransactionStatus.CheckedIn)
            {
                // Driver complete trực tiếp từ CheckedIn (không qua Staff workflow)
                swap.ReturnedBatteryId = null; // Pin khách hàng không có trong hệ thống trạm
                swap.ReturnedBatterySerial = request.ReturnedBatterySerial;
                swap.BatteryHealthReturned = request.BatteryHealthReturned;
                swap.BatteryReturnedAt = DateTime.UtcNow;
            }
            // Nếu status = BatteryReturned, thông tin pin đã được Staff cập nhật rồi
            
            swap.Status = SwapTransactionStatus.Completed;
            swap.CompletedAt = DateTime.UtcNow;
            swap.Notes = string.IsNullOrEmpty(swap.Notes) ? request.Notes : $"{swap.Notes}; {request.Notes}";

            // 4. Increment swap counter for subscription users
            if (swap.UserSubscriptionId.HasValue)
            {
                var subscription = await _context.UserSubscriptions
                    .Include(us => us.SubscriptionPlan)
                    .FirstOrDefaultAsync(us => us.Id == swap.UserSubscriptionId);

                if (subscription != null)
                {
                    subscription.CurrentMonthSwapCount++;
                    
                    _logger.LogInformation(
                        "Incremented swap count for user {UserId}, subscription {SubscriptionId}: {CurrentCount}/{MaxCount}",
                        userId,
                        subscription.Id,
                        subscription.CurrentMonthSwapCount,
                        subscription.SubscriptionPlan.MaxSwapsPerMonth?.ToString() ?? "Unlimited");
                }
            }

            // 5. Update battery statuses
            // Issued battery goes to charging/maintenance
            if (swap.IssuedBattery != null)
            {
                swap.IssuedBattery.Status = BatteryStatus.Charging;
                swap.IssuedBattery.UpdatedAt = DateTime.UtcNow;
            }

            // 6. Keep reservation status as Confirmed when swap completes
            // Reservation remains Confirmed to show it was successfully used

            // ✅ INVOICE REMOVED: Payment tracking handled separately via Payment model

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Swap transaction completed: {TransactionNumber}", swap.TransactionNumber);

            return swap;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SwapHistoryResponse> GetUserSwapHistoryAsync(Guid userId, int page = 1, int pageSize = 10)
    {
        var query = _context.SwapTransactions
            .Include(s => s.Station)
            .Include(s => s.Vehicle)
            .Include(s => s.User)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var transactions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SwapTransactionResponse
            {
                Id = s.Id,
                TransactionNumber = s.TransactionNumber,
                Status = s.Status.ToString(),
                UserEmail = s.User.Email,
                StationName = s.Station.Name,
                StationAddress = s.Station.Address,
                VehicleLicensePlate = s.Vehicle.Plate,
                VehicleModel = s.Vehicle.VIN, // Using VIN as model identifier
                VehicleOdoAtSwap = s.VehicleOdoAtSwap,
                IssuedBatterySerial = s.IssuedBatterySerial,
                ReturnedBatterySerial = s.ReturnedBatterySerial,
                BatteryHealthIssued = s.BatteryHealthIssued,
                BatteryHealthReturned = s.BatteryHealthReturned,
                PaymentType = s.PaymentType.ToString(),
                SwapFee = s.SwapFee,
                KmChargeAmount = s.KmChargeAmount,
                TotalAmount = s.TotalAmount,
                IsPaid = s.IsPaid,
                StartedAt = s.StartedAt,
                CheckedInAt = s.CheckedInAt,
                BatteryIssuedAt = s.BatteryIssuedAt,
                BatteryReturnedAt = s.BatteryReturnedAt,
                CompletedAt = s.CompletedAt,
                Notes = s.Notes,
                ReservationId = s.ReservationId,
                UserSubscriptionId = s.UserSubscriptionId,
                Rating = s.Rating,
                Feedback = s.Feedback,
                RatedAt = s.RatedAt
            })
            .ToListAsync();

        return new SwapHistoryResponse
        {
            Transactions = transactions,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<SwapTransaction> IssueBatteryAsync(Guid swapId, Guid staffId, IssueBatteryRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var swap = await _context.SwapTransactions
                .Include(s => s.Station)
                .Include(s => s.Vehicle)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == swapId);

            if (swap == null)
                throw new InvalidOperationException("Swap transaction not found");

            if (swap.Status != SwapTransactionStatus.CheckedIn)
                throw new InvalidOperationException($"Cannot issue battery. Current status: {swap.Status}");

            // Validate battery exists and is available
            var battery = await _context.BatteryUnits
                .FirstOrDefaultAsync(b => b.Id == request.BatteryUnitId && b.Status == BatteryStatus.Full);

            if (battery == null)
                throw new InvalidOperationException($"Battery unit not found or not available");

            // Update swap transaction
            swap.IssuedBatteryId = battery.Id;
            swap.IssuedBatterySerial = battery.Serial;
            swap.BatteryHealthIssued = 100; // Pin mới luôn 100%
            swap.BatteryIssuedByStaffId = staffId;
            swap.BatteryIssuedAt = DateTime.UtcNow;
            swap.Status = SwapTransactionStatus.BatteryIssued;
            swap.Notes = string.IsNullOrEmpty(swap.Notes) ? request.Notes : $"{swap.Notes}; {request.Notes}";

            // Update battery status
            var oldStatus = battery.Status; // Store old status for inventory sync
            battery.Status = BatteryStatus.Issued;
            battery.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // HYBRID SOLUTION: Sync inventory count (Full -> Issued)
            // This maintains consistency between BatteryUnit and BatteryInventory tables
            try
            {
                await _inventoryService.UpdateInventoryCountAsync(
                    battery.BatteryModelId, 
                    battery.StationId, 
                    oldStatus, 
                    BatteryStatus.Issued, 
                    quantity: 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync inventory for battery {BatterySerial}. Manual reconciliation may be needed.", battery.Serial);
                // Continue - don't fail the main transaction if inventory sync fails
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Battery {BatterySerial} issued for swap {TransactionNumber} by staff {StaffId}", 
                battery.Serial, swap.TransactionNumber, staffId);

            return swap;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SwapTransaction> ReceiveBatteryAsync(Guid swapId, Guid staffId, ReceiveBatteryRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var swap = await _context.SwapTransactions
                .Include(s => s.IssuedBattery)
                .Include(s => s.Vehicle)
                .Include(s => s.Station)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == swapId);

            if (swap == null)
                throw new InvalidOperationException("Swap transaction not found");

            if (swap.Status != SwapTransactionStatus.BatteryIssued)
                throw new InvalidOperationException($"Cannot receive battery. Current status: {swap.Status}");

            // Pin trả về là pin cũ của khách hàng (không có trong database trạm)
            // Chỉ cần lưu thông tin serial và sức khỏe pin, không cần validate tồn tại trong DB
            
            // Update swap transaction
            swap.ReturnedBatteryId = null; // Pin khách hàng không có trong hệ thống trạm
            swap.ReturnedBatterySerial = request.ReturnedBatterySerial;
            swap.BatteryHealthReturned = request.BatteryHealthReturned;
            swap.BatteryReceivedByStaffId = staffId;
            swap.BatteryReturnedAt = DateTime.UtcNow;
            swap.Status = SwapTransactionStatus.BatteryReturned; // Chờ Driver complete
            swap.Notes = string.IsNullOrEmpty(swap.Notes) ? request.Notes : $"{swap.Notes}; {request.Notes}";

            // Pin khách hàng trả về sẽ được xử lý riêng (sạc, bảo trì) ngoài hệ thống

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Battery {BatterySerial} received for swap {TransactionNumber} by staff {StaffId}, waiting for driver to complete", 
                request.ReturnedBatterySerial, swap.TransactionNumber, staffId);

            return swap;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<SwapStatisticsResponse> GetUserSwapStatisticsAsync(Guid userId)
    {
        var allSwaps = await _context.SwapTransactions
            .Include(s => s.Station)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        if (!allSwaps.Any())
        {
            return new SwapStatisticsResponse();
        }

        var completedSwaps = allSwaps.Where(s => s.Status == SwapTransactionStatus.Completed).ToList();
        var cancelledSwaps = allSwaps.Where(s => s.Status == SwapTransactionStatus.Cancelled).ToList();
        var failedSwaps = allSwaps.Where(s => s.Status == SwapTransactionStatus.Failed).ToList();

        // Thống kê trạm được sử dụng nhiều nhất
        var stationUsage = allSwaps
            .GroupBy(s => new { s.StationId, s.Station.Name })
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        // Tính toán số ngày từ lần đổi đầu tiên
        var firstSwap = allSwaps.LastOrDefault();
        var lastSwap = allSwaps.FirstOrDefault();
        var daysSinceFirst = firstSwap != null ? (DateTime.UtcNow - firstSwap.StartedAt).Days : 0;
        var monthsSinceFirst = Math.Max(1, daysSinceFirst / 30.0);

        // Thống kê theo thời gian gần đây
        var now = DateTime.UtcNow;
        var swapsLast7Days = allSwaps.Count(s => s.StartedAt >= now.AddDays(-7));
        var swapsLast30Days = allSwaps.Count(s => s.StartedAt >= now.AddDays(-30));

        return new SwapStatisticsResponse
        {
            // Thống kê tổng quan
            TotalSwaps = allSwaps.Count,
            CompletedSwaps = completedSwaps.Count,
            CancelledSwaps = cancelledSwaps.Count,
            FailedSwaps = failedSwaps.Count,
            SuccessRate = allSwaps.Count > 0 ? Math.Round((decimal)completedSwaps.Count / allSwaps.Count * 100, 2) : 0,

            // Thống kê tài chính
            TotalAmount = completedSwaps.Sum(s => s.TotalAmount),
            AverageSwapFee = completedSwaps.Any() ? Math.Round(completedSwaps.Average(s => s.SwapFee), 0) : 0,
            TotalKmCharges = completedSwaps.Sum(s => s.KmChargeAmount),

            // Thống kê xe và pin
            TotalKilometers = completedSwaps.Sum(s => s.VehicleOdoAtSwap),
            AverageKmPerSwap = completedSwaps.Any() ? (int)Math.Round(completedSwaps.Average(s => s.VehicleOdoAtSwap)) : 0,
            AverageBatteryHealthIssued = completedSwaps.Where(s => s.BatteryHealthIssued.HasValue).Any() ? 
                (int)Math.Round(completedSwaps.Where(s => s.BatteryHealthIssued.HasValue).Average(s => s.BatteryHealthIssued!.Value)) : 0,
            AverageBatteryHealthReturned = completedSwaps.Where(s => s.BatteryHealthReturned.HasValue).Any() ? 
                (int)Math.Round(completedSwaps.Where(s => s.BatteryHealthReturned.HasValue).Average(s => s.BatteryHealthReturned!.Value)) : 0,

            // Thống kê thời gian
            FirstSwapDate = firstSwap?.StartedAt,
            LastSwapDate = lastSwap?.StartedAt,
            DaysSinceFirstSwap = daysSinceFirst,
            AverageSwapsPerMonth = Math.Round(allSwaps.Count / monthsSinceFirst, 1),

            // Thống kê trạm
            MostUsedStationName = stationUsage?.Key.Name,
            MostUsedStationCount = stationUsage?.Count() ?? 0,

            // Feedback và đánh giá
            AverageRating = completedSwaps.Where(s => s.Rating.HasValue).Any() ? 
                Math.Round(completedSwaps.Where(s => s.Rating.HasValue).Average(s => s.Rating!.Value), 1) : null,
            TotalFeedbacks = completedSwaps.Count(s => !string.IsNullOrEmpty(s.Feedback)),

            // Thống kê gần đây
            SwapsLast30Days = swapsLast30Days,
            SwapsLast7Days = swapsLast7Days
        };
    }

    public async Task<SwapTransaction> RateSwapAsync(Guid swapId, Guid userId, SwapRatingRequest request)
    {
        var swap = await _context.SwapTransactions
            .FirstOrDefaultAsync(s => s.Id == swapId && s.UserId == userId);

        if (swap == null)
            throw new InvalidOperationException("Swap transaction not found or does not belong to user");

        if (swap.Status != SwapTransactionStatus.Completed)
            throw new InvalidOperationException("Can only rate completed swaps");

        if (request.Rating < 1 || request.Rating > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5");

        swap.Rating = request.Rating;
        swap.Feedback = request.Feedback;
        swap.RatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Swap {TransactionNumber} rated {Rating} stars by user {UserId}", 
            swap.TransactionNumber, request.Rating, userId);

        return swap;
    }

    private async Task<string> GenerateTransactionNumberAsync()
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"EVB-SWT-{today:yyyyMMdd}";
        
        var lastTransaction = await _context.SwapTransactions
            .Where(s => s.TransactionNumber.StartsWith(prefix))
            .OrderByDescending(s => s.TransactionNumber)
            .FirstOrDefaultAsync();
        
        int nextNumber = 1;
        if (lastTransaction != null)
        {
            var lastNumberStr = lastTransaction.TransactionNumber.Substring(prefix.Length);
            if (int.TryParse(lastNumberStr, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }
        
        return $"{prefix}{nextNumber:D4}";
    }

    private async Task CalculateSwapFeesAsync(SwapTransaction swap, UserSubscription? subscription)
    {
        if (subscription != null)
        {
            // Subscription-based pricing
            swap.PaymentType = PaymentType.Subscription;
            swap.SwapFee = 0; // Free swaps with subscription
            
            // Calculate km-based charges if applicable
            var plan = subscription.SubscriptionPlan;
            // For now, subscription doesn't charge per km in our model
            // This would be calculated based on subscription plan tiers
            swap.KmChargeAmount = 0;
            
            swap.TotalAmount = swap.SwapFee + swap.KmChargeAmount;
        }
        else
        {
            // Per-swap pricing - get from station or default rate
            swap.PaymentType = PaymentType.PayPerSwap;
            swap.SwapFee = await GetPerSwapFeeAsync(swap.StationId);
            swap.KmChargeAmount = 0;
            swap.TotalAmount = swap.SwapFee;
        }
    }

    private Task<decimal> GetPerSwapFeeAsync(Guid stationId)
    {
        // Get per-swap fee from station configuration or use default
        // For now, return a default value - this could be configurable per station
        return Task.FromResult(50000m); // 50,000 VND per swap
    }

    // ✅ INVOICE METHODS REMOVED: Using simplified Payment model instead
}