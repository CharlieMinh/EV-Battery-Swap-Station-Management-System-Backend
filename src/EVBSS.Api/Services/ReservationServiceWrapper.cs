using EVBSS.Api.Data;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

/// <summary>
/// DEPRECATED: Legacy reservation service wrapper
/// All new development should use SlotReservationService
/// </summary>
public class ReservationService
{
    private readonly AppDbContext _db;
    private readonly SlotReservationService _slotService;

    public ReservationService(AppDbContext db, SlotReservationService slotService)
    {
        _db = db;
        _slotService = slotService;
    }

    // Legacy method - now delegates to slot service
    public async Task<List<Reservation>> ListMineAsync(Guid userId, ReservationStatus? status)
    {
        var q = _db.Reservations.AsNoTracking().Where(r => r.UserId == userId);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    // Legacy method - now handled by background service
    public async Task<int> ExpireOverduesAsync()
    {
        return await _slotService.ExpireOverdueReservationsAsync();
    }
}
