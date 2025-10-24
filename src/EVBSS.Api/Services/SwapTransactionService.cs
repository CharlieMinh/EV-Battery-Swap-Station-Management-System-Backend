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

    public async Task<SwapTransaction> FinalizeFromReservationAsync(FinalizeSwapRequest request, Guid staffId)
    {
        using var dbTransaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Find and validate the reservation
            var reservation = await _context.Reservations
                .Include(r => r.User).ThenInclude(u => u.Vehicles)
                .Include(r => r.Station)
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId);

            if (reservation == null)
                throw new KeyNotFoundException("Không tìm thấy lịch hẹn.");

            if (reservation.Status != ReservationStatus.CheckedIn)
                throw new InvalidOperationException($"Lịch hẹn phải ở trạng thái 'CheckedIn' để hoàn tất. Trạng thái hiện tại: {reservation.Status}");

            if (!reservation.BatteryUnitId.HasValue)
                throw new InvalidOperationException("Lịch hẹn chưa được gán pin mới.");

            // 2. Find the new and old batteries
            var newBattery = await _context.BatteryUnits.FindAsync(reservation.BatteryUnitId.Value);
            if (newBattery == null)
                throw new InvalidOperationException("Không tìm thấy thông tin pin mới đã gán.");

            var oldBattery = await _context.BatteryUnits
                .FirstOrDefaultAsync(b => b.Serial == request.OldBatterySerial);

            var vehicle = reservation.User.Vehicles.FirstOrDefault();
            if (vehicle == null)
                throw new InvalidOperationException("Không tìm thấy thông tin xe của người dùng.");

            // 3. Create the SwapTransaction
            var swapTransaction = new SwapTransaction
            {
                UserId = reservation.UserId,
                StationId = reservation.StationId,
                ReservationId = reservation.Id,
                VehicleId = vehicle.Id,
                IssuedBatteryId = newBattery.Id,
                ReturnedBatteryId = oldBattery?.Id,
                IssuedBatterySerial = newBattery.Serial,
                ReturnedBatterySerial = request.OldBatterySerial,
                Status = SwapTransactionStatus.Completed,
                CompletedAt = DateTime.UtcNow,
                CompletedByStaffId = staffId,
                PaymentType = reservation.Payment != null ? PaymentType.PayPerSwap : PaymentType.Subscription,
                TotalAmount = reservation.Payment?.Amount ?? 0,
                IsPaid = reservation.Payment?.Status == PaymentStatus.Completed || reservation.Payment == null,
                StartedAt = reservation.CreatedAt,
                CheckedInAt = reservation.CheckedInAt,
                BatteryIssuedAt = DateTime.UtcNow,
            };

            _context.SwapTransactions.Add(swapTransaction);

            // 4. Update reservation status
            reservation.Status = ReservationStatus.Completed;

            // 5. Update battery statuses
            newBattery.Status = BatteryStatus.InUse;

            if (oldBattery != null)
            {
                oldBattery.Status = BatteryStatus.Depleted;
                oldBattery.StationId = reservation.StationId; // It's now at this station
                oldBattery.UpdatedAt = DateTime.UtcNow;
            }

            // 6. Update subscription swap count if applicable
            if (reservation.Payment == null)
            {
                var activeSubscription = await _context.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == reservation.UserId && s.IsActive);
                
                if (activeSubscription != null)
                {
                    activeSubscription.CurrentMonthSwapCount++;
                    swapTransaction.UserSubscriptionId = activeSubscription.Id;
                    _logger.LogInformation("Incremented swap count for subscription {SubscriptionId} to {SwapCount}", 
                        activeSubscription.Id, activeSubscription.CurrentMonthSwapCount);
                }
                else
                {
                    _logger.LogWarning("Could not find active subscription for user {UserId} to increment swap count on a non-payment reservation {ReservationId}.",
                        reservation.UserId, reservation.Id);
                }
            }

            // 7. Save all changes
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Successfully finalized swap from reservation {ReservationId}. New SwapTransaction ID: {SwapTransactionId}", 
                request.ReservationId, swapTransaction.Id);

            return swapTransaction;
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error finalizing swap from reservation {ReservationId}", request.ReservationId);
            throw; // Re-throw the exception to be caught by the controller
        }
    }

    // ... Other existing methods of the service ...
}