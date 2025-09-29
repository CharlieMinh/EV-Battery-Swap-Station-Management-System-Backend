using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.SwapTransactions;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

public class SwapTransactionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SwapTransactionService> _logger;

    public SwapTransactionService(AppDbContext context, ILogger<SwapTransactionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SwapTransaction> StartSwapFromReservationAsync(Guid userId, StartSwapRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Validate reservation
            var reservation = await _context.Reservations
                .Include(r => r.Station)
                .Include(r => r.BatteryUnit)
                .Include(r => r.BatteryModel)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId && r.UserId == userId);

            if (reservation == null)
                throw new InvalidOperationException("Reservation not found or does not belong to user");

            if (reservation.Status != ReservationStatus.Held)
                throw new InvalidOperationException($"Reservation status is {reservation.Status}, cannot start swap");

            // Check if reservation is still valid (not expired)
            var expiryTime = reservation.CreatedAt.AddMinutes(reservation.HoldDurationMinutes);
            if (DateTime.UtcNow > expiryTime)
                throw new InvalidOperationException("Reservation has expired");

            // 2. Validate vehicle belongs to user
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.UserId == userId);

            if (vehicle == null)
                throw new InvalidOperationException("Vehicle not found or does not belong to user");

            // 3. Check if vehicle is compatible with reserved battery
            if (vehicle.CompatibleBatteryModelId != reservation.BatteryModelId)
                throw new InvalidOperationException("Vehicle battery model does not match reserved battery model");

            // 4. Get user's active subscription (if any)
            var activeSubscription = await _context.UserSubscriptions
                .Include(us => us.SubscriptionPlan)
                .FirstOrDefaultAsync(us => us.UserId == userId && 
                                         us.IsActive == true && 
                                         us.StartDate <= DateTime.UtcNow && 
                                         (us.EndDate == null || us.EndDate > DateTime.UtcNow));

            // 5. Generate transaction number
            var transactionNumber = await GenerateTransactionNumberAsync();

            // 6. Create swap transaction
            var swapTransaction = new SwapTransaction
            {
                TransactionNumber = transactionNumber,
                UserId = userId,
                ReservationId = reservation.Id,
                StationId = reservation.StationId,
                VehicleId = request.VehicleId,
                UserSubscriptionId = activeSubscription?.Id,
                IssuedBatteryId = reservation.BatteryUnitId,
                IssuedBatterySerial = reservation.BatteryUnit.Serial,
                VehicleOdoAtSwap = request.VehicleOdometer,
                BatteryHealthIssued = 90, // Default battery health - would be stored in BatteryUnit
                PaymentType = activeSubscription != null ? PaymentType.Subscription : PaymentType.PayPerSwap,
                Status = SwapTransactionStatus.CheckedIn,
                StartedAt = DateTime.UtcNow,
                CheckedInAt = DateTime.UtcNow,
                Notes = request.Notes
            };

            // 7. Calculate fees
            await CalculateSwapFeesAsync(swapTransaction, activeSubscription);

            _context.SwapTransactions.Add(swapTransaction);

            // 8. Update reservation status to Confirmed when swap starts
            reservation.Status = ReservationStatus.Confirmed;

            // 9. Update battery unit status to Issued
            reservation.BatteryUnit.Status = BatteryStatus.Issued;
            reservation.BatteryUnit.UpdatedAt = DateTime.UtcNow;

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

            _logger.LogInformation("Swap transaction started: {TransactionNumber} for user {UserId}", 
                transactionNumber, userId);

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

            if (swap.Status != SwapTransactionStatus.CheckedIn)
                throw new InvalidOperationException($"Swap status is {swap.Status}, cannot complete");

            // 2. Find returned battery by serial number
            var returnedBattery = await _context.BatteryUnits
                .FirstOrDefaultAsync(b => b.Serial == request.ReturnedBatterySerial);

            if (returnedBattery == null)
                throw new InvalidOperationException($"Battery with serial {request.ReturnedBatterySerial} not found");

            // 3. Validate returned battery model matches vehicle
            if (returnedBattery.BatteryModelId != swap.Vehicle.CompatibleBatteryModelId)
                throw new InvalidOperationException("Returned battery model does not match vehicle requirements");

            // 4. Update swap transaction
            swap.ReturnedBatteryId = returnedBattery.Id;
            swap.ReturnedBatterySerial = request.ReturnedBatterySerial;
            swap.BatteryHealthReturned = request.BatteryHealthReturned;
            swap.Status = SwapTransactionStatus.Completed;
            swap.BatteryReturnedAt = DateTime.UtcNow;
            swap.CompletedAt = DateTime.UtcNow;
            swap.Notes = string.IsNullOrEmpty(swap.Notes) ? request.Notes : $"{swap.Notes}; {request.Notes}";

            // 5. Update battery statuses
            // Issued battery goes to charging/maintenance
            swap.IssuedBattery.Status = BatteryStatus.Charging;
            swap.IssuedBattery.UpdatedAt = DateTime.UtcNow;
            
            // Returned battery becomes full and ready for next use
            returnedBattery.Status = BatteryStatus.Full;
            returnedBattery.UpdatedAt = DateTime.UtcNow;

            // 6. Keep reservation status as Confirmed when swap completes
            // Reservation remains Confirmed to show it was successfully used

            // 7. Create invoice if needed
            await CreateInvoiceIfNeededAsync(swap);

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
                UserSubscriptionId = s.UserSubscriptionId
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

    private Task CreateInvoiceIfNeededAsync(SwapTransaction swap)
    {
        if (swap.TotalAmount > 0 && !swap.IsPaid)
        {
            var invoice = new Invoice
            {
                UserId = swap.UserId,
                Type = InvoiceType.SwapTransaction,
                TotalAmount = swap.TotalAmount,
                SubtotalAmount = swap.TotalAmount,
                Status = PaymentStatus.Pending,
                DueDate = DateTime.UtcNow.AddDays(7), // 7 days to pay
                Notes = $"Battery swap fee - {swap.TransactionNumber}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            swap.InvoiceId = invoice.Id;
        }
        return Task.CompletedTask;
    }
}