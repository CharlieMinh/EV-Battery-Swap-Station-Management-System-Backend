using EVBSS.Api.Data;
using EVBSS.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace EVBSS.Api.Services;

public class NoBatteryException : Exception
{
    public NoBatteryException() : base("No full battery available.") {}
}

public class ReservationService
{
    private readonly AppDbContext _db;
    public ReservationService(AppDbContext db) => _db = db;

    public async Task<(Reservation Reservation, DateTime ExpiresAt)> HoldAsync(
        Guid userId, Guid stationId, Guid batteryModelId, DateTime startTime, int holdMinutes = 15)
    {
        // Transaction + khóa hàng để tránh race
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        // 1) chọn 1 viên Full & !IsReserved bằng SELECT có lock hint
        var unit = await _db.BatteryUnits
            .FromSqlInterpolated($@"
                SELECT TOP(1) * FROM dbo.BatteryUnits WITH (UPDLOCK, ROWLOCK, READPAST)
                WHERE StationId = {stationId}
                  AND BatteryModelId = {batteryModelId}
                  AND Status = {(int)BatteryStatus.Full}
                  AND IsReserved = 0
                ORDER BY UpdatedAt ASC")
            .AsTracking()
            .FirstOrDefaultAsync();

        if (unit is null)
            throw new NoBatteryException();

        // 2) đánh dấu giữ
        unit.IsReserved = true;
        unit.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // 3) tạo reservation
        var res = new Reservation
        {
            UserId = userId,
            StationId = stationId,
            BatteryModelId = batteryModelId,
            BatteryUnitId = unit.Id,
            StartTime = startTime,
            Status = ReservationStatus.Held,
            HoldDurationMinutes = holdMinutes,
            CreatedAt = DateTime.UtcNow
        };
        _db.Reservations.Add(res);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        var expiresAt = res.CreatedAt.AddMinutes(res.HoldDurationMinutes);
        return (res, expiresAt);
    }

    public async Task<List<Reservation>> ListMineAsync(Guid userId, ReservationStatus? status)
    {
        var q = _db.Reservations.AsNoTracking().Where(r => r.UserId == userId);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task CancelAsync(Guid userId, Guid reservationId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var res = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId);
        if (res is null) throw new KeyNotFoundException("RESERVATION_NOT_FOUND");

        if (res.Status == ReservationStatus.Held)
        {
            // trả pin
            var unit = await _db.BatteryUnits.FirstOrDefaultAsync(u => u.Id == res.BatteryUnitId);
            if (unit != null)
            {
                unit.IsReserved = false;
                unit.UpdatedAt = DateTime.UtcNow;
            }
            res.Status = ReservationStatus.Cancelled;
            await _db.SaveChangesAsync();
        }

        await tx.CommitAsync();
    }

    // Dev-only: expire Held quá hạn
    public async Task<int> ExpireOverduesAsync()
    {
        int changed = 0;
        var now = DateTime.UtcNow;


    // Lấy danh sách Held đã quá hạn: số phút từ CreatedAt đến 'now' >= HoldDurationMinutes
    var overdue = await _db.Reservations
        .Where(r => r.Status == ReservationStatus.Held
            && EF.Functions.DateDiffMinute(r.CreatedAt, now) >= r.HoldDurationMinutes)
        .Select(r => new { r.Id, r.BatteryUnitId })
        .ToListAsync();

    foreach (var item in overdue)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        try
        {
            var res = await _db.Reservations.FirstAsync(x => x.Id == item.Id);
            var unit = await _db.BatteryUnits.FirstOrDefaultAsync(u => u.Id == item.BatteryUnitId);
            if (unit != null)
            {
                unit.IsReserved = false;
                unit.UpdatedAt = DateTime.UtcNow;
            }
            res.Status = ReservationStatus.Cancelled;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            changed++;
        }
        catch
        {
            await tx.RollbackAsync();
        }
    }

    return changed;
    }
}
