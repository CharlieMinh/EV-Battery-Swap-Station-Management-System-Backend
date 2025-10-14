using EVBSS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

/// <summary>
/// Service quản lý Station với tự động tạo DisplayId theo format T01, T02, T03...
/// </summary>
public class StationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<StationService> _logger;

    public StationService(AppDbContext context, ILogger<StationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Tự động tạo DisplayId tiếp theo theo format T01, T02, T03...
    /// </summary>
    public async Task<string> GenerateNextDisplayIdAsync()
    {
        // Lấy tất cả DisplayId hiện tại và tìm số lớn nhất
        var existingDisplayIds = await _context.Stations
            .Where(s => s.DisplayId != null)
            .Select(s => s.DisplayId)
            .ToListAsync();

        int maxNumber = 0;
        foreach (var displayId in existingDisplayIds)
        {
            // DisplayId format: T01, T02, T03...
            if (displayId != null && displayId.StartsWith("T") && displayId.Length > 1)
            {
                if (int.TryParse(displayId.Substring(1), out int number))
                {
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }
        }

        // Tạo DisplayId tiếp theo
        int nextNumber = maxNumber + 1;
        return $"T{nextNumber:D2}"; // Format: T01, T02, T03... T99
    }

    /// <summary>
    /// Tạo Station mới với DisplayId tự động
    /// </summary>
    public async Task<Models.Station> CreateStationAsync(Models.Station station)
    {
        // Nếu chưa có DisplayId, tự động tạo
        if (string.IsNullOrEmpty(station.DisplayId))
        {
            station.DisplayId = await GenerateNextDisplayIdAsync();
        }

        _context.Stations.Add(station);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created station {StationId} with DisplayId {DisplayId}", 
            station.Id, station.DisplayId);

        return station;
    }

    /// <summary>
    /// Cập nhật DisplayId cho các Station hiện có chưa có DisplayId
    /// Dùng khi khởi động app lần đầu sau khi thêm feature này
    /// </summary>
    public async Task UpdateExistingStationsDisplayIdAsync()
    {
        var stationsWithoutDisplayId = await _context.Stations
            .Where(s => s.DisplayId == null)
            .OrderBy(s => s.Name) // Sắp xếp theo tên để có thứ tự nhất quán
            .ToListAsync();

        if (!stationsWithoutDisplayId.Any())
        {
            _logger.LogInformation("All stations already have DisplayId");
            return;
        }

        // Tìm số DisplayId lớn nhất hiện có
        var existingDisplayIds = await _context.Stations
            .Where(s => s.DisplayId != null)
            .Select(s => s.DisplayId)
            .ToListAsync();

        int maxNumber = 0;
        foreach (var displayId in existingDisplayIds)
        {
            if (displayId != null && displayId.StartsWith("T") && displayId.Length > 1)
            {
                if (int.TryParse(displayId.Substring(1), out int number))
                {
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }
        }

        // Tạo DisplayId cho từng station (T01, T02, T03...)
        int currentNumber = maxNumber;
        foreach (var station in stationsWithoutDisplayId)
        {
            currentNumber++;
            station.DisplayId = $"T{currentNumber:D2}";
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated DisplayId for {Count} stations", 
            stationsWithoutDisplayId.Count);
    }
}
