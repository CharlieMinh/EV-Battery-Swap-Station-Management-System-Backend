using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Reports;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Roles = "Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminReportsController(AppDbContext db) => _db = db;

    // GET /api/v1/admin/reports/overview
    [HttpGet("overview")]
    public async Task<ActionResult<SystemOverviewDto>> GetSystemOverview()
    {
        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.LastLogin.HasValue && u.LastLogin.Value > DateTime.UtcNow.AddDays(-30));
        var totalStations = await _db.Stations.CountAsync();
        var activeStations = await _db.Stations.CountAsync(s => s.IsActive);
        var totalBatteries = await _db.BatteryUnits.CountAsync();
        var availableBatteries = await _db.BatteryUnits.CountAsync(b => b.Status == BatteryStatus.Full && !b.IsReserved);

        var today = DateTime.UtcNow.Date;
        var todaySwaps = await _db.SwapTransactions.CountAsync(st => st.SwapTime.Date == today && st.Status == SwapStatus.Completed);
        var todayRevenue = await _db.SwapTransactions
            .Where(st => st.SwapTime.Date == today && st.Status == SwapStatus.Completed && st.Cost.HasValue)
            .SumAsync(st => st.Cost!.Value);

        var activeSubscriptions = await _db.UserSubscriptions.CountAsync(us => us.Status == UserSubscriptionStatus.Active && us.EndDate > DateTime.UtcNow);

        return new SystemOverviewDto(
            totalUsers,
            activeUsers,
            totalStations,
            activeStations,
            totalBatteries,
            availableBatteries,
            todaySwaps,
            todayRevenue,
            activeSubscriptions,
            DateTime.UtcNow
        );
    }

    // GET /api/v1/admin/reports/daily-swaps?from=2024-01-01&to=2024-01-31
    [HttpGet("daily-swaps")]
    public async Task<ActionResult<IReadOnlyList<DailySwapReportDto>>> GetDailySwapReport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var toDate = to?.Date ?? DateTime.UtcNow.Date;

        if (toDate < fromDate)
            return BadRequest(new { error = new { code = "INVALID_DATE_RANGE", message = "To date must be greater than or equal to from date" } });

        var swapData = await _db.SwapTransactions
            .Where(st => st.SwapTime.Date >= fromDate && st.SwapTime.Date <= toDate)
            .GroupBy(st => st.SwapTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalSwaps = g.Count(),
                SuccessfulSwaps = g.Count(st => st.Status == SwapStatus.Completed),
                FailedSwaps = g.Count(st => st.Status == SwapStatus.Failed),
                TotalRevenue = g.Where(st => st.Status == SwapStatus.Completed && st.Cost.HasValue).Sum(st => st.Cost!.Value),
                UniqueUsers = g.Select(st => st.UserId).Distinct().Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var result = swapData.Select(x => new DailySwapReportDto(
            x.Date,
            x.TotalSwaps,
            x.SuccessfulSwaps,
            x.FailedSwaps,
            x.TotalRevenue,
            x.UniqueUsers
        )).ToList();

        return result;
    }

    // GET /api/v1/admin/reports/revenue?from=2024-01-01&to=2024-01-31
    [HttpGet("revenue")]
    public async Task<ActionResult<IReadOnlyList<RevenueReportDto>>> GetRevenueReport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var toDate = to?.Date ?? DateTime.UtcNow.Date;

        if (toDate < fromDate)
            return BadRequest(new { error = new { code = "INVALID_DATE_RANGE", message = "To date must be greater than or equal to from date" } });

        // Swap revenue by date
        var swapRevenue = await _db.SwapTransactions
            .Where(st => st.SwapTime.Date >= fromDate && st.SwapTime.Date <= toDate && st.Status == SwapStatus.Completed && st.Cost.HasValue)
            .GroupBy(st => st.SwapTime.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(st => st.Cost!.Value) })
            .ToListAsync();

        // Subscription revenue by date (when subscription starts)
        var subscriptionRevenue = await _db.UserSubscriptions
            .Include(us => us.SubscriptionPlan)
            .Where(us => us.StartDate.Date >= fromDate && us.StartDate.Date <= toDate)
            .GroupBy(us => us.StartDate.Date)
            .Select(g => new 
            { 
                Date = g.Key, 
                Revenue = g.Sum(us => us.SubscriptionPlan.Price),
                NewSubscriptions = g.Count(),
                ActiveSubscriptions = g.Count(us => us.Status == UserSubscriptionStatus.Active)
            })
            .ToListAsync();

        // Combine data for each date
        var allDates = swapRevenue.Select(sr => sr.Date)
            .Union(subscriptionRevenue.Select(sr => sr.Date))
            .Distinct()
            .OrderBy(d => d);

        var result = allDates.Select(date =>
        {
            var swapRev = swapRevenue.FirstOrDefault(sr => sr.Date == date)?.Revenue ?? 0;
            var subRev = subscriptionRevenue.FirstOrDefault(sr => sr.Date == date)?.Revenue ?? 0;
            var newSubs = subscriptionRevenue.FirstOrDefault(sr => sr.Date == date)?.NewSubscriptions ?? 0;
            var activeSubs = subscriptionRevenue.FirstOrDefault(sr => sr.Date == date)?.ActiveSubscriptions ?? 0;

            return new RevenueReportDto(
                date,
                subRev,
                swapRev,
                subRev + swapRev,
                newSubs,
                activeSubs
            );
        }).ToList();

        return result;
    }

    // GET /api/v1/admin/reports/inventory
    [HttpGet("inventory")]
    public async Task<ActionResult<IReadOnlyList<InventoryReportDto>>> GetInventoryReport()
    {
        var stations = await _db.Stations.AsNoTracking().ToListAsync();
        var result = new List<InventoryReportDto>();

        foreach (var station in stations)
        {
            var batteryStats = await _db.BatteryUnits
                .Where(b => b.StationId == station.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Full = g.Count(b => b.Status == BatteryStatus.Full),
                    Charging = g.Count(b => b.Status == BatteryStatus.Charging),
                    Maintenance = g.Count(b => b.Status == BatteryStatus.Maintenance)
                })
                .FirstOrDefaultAsync();

            var total = batteryStats?.Total ?? 0;
            var available = batteryStats?.Full ?? 0;
            var charging = batteryStats?.Charging ?? 0;
            var maintenance = batteryStats?.Maintenance ?? 0;

            // Calculate utilization rate (batteries that are used vs total)
            var utilizedBatteries = total - maintenance;
            var utilizationRate = total > 0 ? Math.Round((double)utilizedBatteries / total * 100, 1) : 0;

            result.Add(new InventoryReportDto(
                station.Id,
                station.Name,
                station.City,
                total,
                available,
                charging,
                maintenance,
                utilizationRate,
                DateTime.UtcNow
            ));
        }

        return result.OrderByDescending(i => i.UtilizationRate).ToList();
    }

    // GET /api/v1/admin/reports/top-stations?limit=10&from=2024-01-01&to=2024-01-31
    [HttpGet("top-stations")]
    public async Task<ActionResult<IReadOnlyList<TopStationDto>>> GetTopStations(
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var fromDate = from?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var toDate = to?.Date ?? DateTime.UtcNow.Date;

        var stationStats = await _db.SwapTransactions
            .Include(st => st.Station)
            .Where(st => st.SwapTime.Date >= fromDate && st.SwapTime.Date <= toDate && st.Status == SwapStatus.Completed)
            .GroupBy(st => new { st.StationId, st.Station.Name, st.Station.City })
            .Select(g => new
            {
                StationId = g.Key.StationId,
                StationName = g.Key.Name,
                City = g.Key.City,
                SwapCount = g.Count(),
                Revenue = g.Where(st => st.Cost.HasValue).Sum(st => st.Cost!.Value)
            })
            .OrderByDescending(x => x.SwapCount)
            .Take(limit)
            .ToListAsync();

        var result = new List<TopStationDto>();
        foreach (var stat in stationStats)
        {
            // Calculate utilization rate for the station
            var totalBatteries = await _db.BatteryUnits.CountAsync(b => b.StationId == stat.StationId);
            var utilizationRate = totalBatteries > 0 ? Math.Round((double)stat.SwapCount / totalBatteries / (toDate - fromDate).Days * 100, 1) : 0;

            result.Add(new TopStationDto(
                stat.StationId,
                stat.StationName,
                stat.City,
                stat.SwapCount,
                stat.Revenue,
                utilizationRate
            ));
        }

        return result;
    }

    // GET /api/v1/admin/reports/swap-trends?from=2024-01-01&to=2024-01-31
    [HttpGet("swap-trends")]
    public async Task<ActionResult<IReadOnlyList<SwapTrendDto>>> GetSwapTrends(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var toDate = to?.Date ?? DateTime.UtcNow.Date;

        var swapTrends = await _db.SwapTransactions
            .Where(st => st.SwapTime.Date >= fromDate && st.SwapTime.Date <= toDate && st.Status == SwapStatus.Completed)
            .GroupBy(st => st.SwapTime.Date)
            .Select(g => new
            {
                Date = g.Key,
                SwapCount = g.Count(),
                HourlyData = g.GroupBy(st => st.SwapTime.Hour)
                              .Select(h => new { Hour = h.Key, Count = h.Count() })
                              .OrderByDescending(h => h.Count)
                              .FirstOrDefault()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var result = swapTrends.Select(st => new SwapTrendDto(
            st.Date,
            st.SwapCount,
            st.HourlyData?.Count ?? 0,
            st.HourlyData != null ? $"{st.HourlyData.Hour}:00" : "N/A"
        )).ToList();

        return result;
    }
}