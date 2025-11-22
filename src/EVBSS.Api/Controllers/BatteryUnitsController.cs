using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.BatteryUnits;
using EVBSS.Api.Dtos.Common;
using Microsoft.AspNetCore.Authorization;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BatteryUnitsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<BatteryUnitsController> _logger;

    public BatteryUnitsController(AppDbContext context, ILogger<BatteryUnitsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Helper to consistently check if a status means the battery is available for swapping.
    private bool IsBatteryAvailable(BatteryStatus status)
    {
        // Only 'Full' batteries are considered available in the station's active inventory.
        return status == BatteryStatus.Full;
    }

    /// <summary>
    /// Lấy danh sách tất cả pin (Public - không cần đăng nhập)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public")]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> GetBatteryUnitsPublic()
    {
        try
        {
            var batteryUnits = await _context.BatteryUnits
                .Include(b => b.Model)
                .Include(b => b.Station)
                .Select(b => new BatteryUnitResponseDto
                {
                    Id = b.Id,
                    Serial = b.Serial,
                    BatteryModelId = b.BatteryModelId,
                    BatteryModelName = b.Model.Name,
                    Voltage = b.Model.Voltage,
                    CapacityWh = b.Model.CapacityWh,
                    Manufacturer = b.Model.Manufacturer,
                    StationId = b.StationId,
                    StationName = b.Station!.Name,
                    Status = b.Status.ToString(),
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = true,
                Data = batteryUnits,
                Message = "Retrieved battery units successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving battery units");
            return StatusCode(500, new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả pin (Yêu cầu xác thực)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> GetBatteryUnits()
    {
        try
        {
            var batteryUnits = await _context.BatteryUnits
                .Include(b => b.Model)
                .Include(b => b.Station)
                .Select(b => new BatteryUnitResponseDto
                {
                    Id = b.Id,
                    Serial = b.Serial,
                    BatteryModelId = b.BatteryModelId,
                    BatteryModelName = b.Model.Name,
                    Voltage = b.Model.Voltage,
                    CapacityWh = b.Model.CapacityWh,
                    Manufacturer = b.Model.Manufacturer,
                    StationId = b.StationId,
                    StationName = b.Station!.Name,
                    Status = b.Status.ToString(),
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = true,
                Data = batteryUnits,
                Message = "Retrieved battery units successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving battery units");
            return StatusCode(500, new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Lấy danh sách pin theo station ID (Public - không cần đăng nhập)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public/station/{stationId}")]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> GetBatteryUnitsByStationPublic(Guid stationId)
    {
        try
        {
            var stationExists = await _context.Stations.AnyAsync(s => s.Id == stationId);
            if (!stationExists)
            {
                return NotFound(new ApiResponse<List<BatteryUnitResponseDto>>
                {
                    Success = false,
                    Message = "Station not found"
                });
            }

            var batteryUnits = await _context.BatteryUnits
                .Include(b => b.Model)
                .Include(b => b.Station)
                .Where(b => b.StationId == stationId)
                .Select(b => new BatteryUnitResponseDto
                {
                    Id = b.Id,
                    Serial = b.Serial,
                    BatteryModelId = b.BatteryModelId,
                    BatteryModelName = b.Model.Name,
                    Voltage = b.Model.Voltage,
                    CapacityWh = b.Model.CapacityWh,
                    Manufacturer = b.Model.Manufacturer,
                    StationId = b.StationId,
                    StationName = b.Station!.Name,
                    Status = b.Status.ToString(),
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = true,
                Data = batteryUnits,
                Message = "Retrieved battery units for station successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving battery units for station {StationId}", stationId);
            return StatusCode(500, new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Lấy danh sách pin theo station ID (Yêu cầu xác thực)
    /// </summary>
    [HttpGet("station/{stationId}")]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> GetBatteryUnitsByStation(Guid stationId)
    {
        try
        {
            var stationExists = await _context.Stations.AnyAsync(s => s.Id == stationId);
            if (!stationExists)
            {
                return NotFound(new ApiResponse<List<BatteryUnitResponseDto>>
                {
                    Success = false,
                    Message = "Station not found"
                });
            }

            var batteryUnits = await _context.BatteryUnits
                .Include(b => b.Model)
                .Include(b => b.Station)
                .Where(b => b.StationId == stationId)
                .Select(b => new BatteryUnitResponseDto
                {
                    Id = b.Id,
                    Serial = b.Serial,
                    BatteryModelId = b.BatteryModelId,
                    BatteryModelName = b.Model.Name,
                    Voltage = b.Model.Voltage,
                    CapacityWh = b.Model.CapacityWh,
                    Manufacturer = b.Model.Manufacturer,
                    StationId = b.StationId,
                    StationName = b.Station!.Name,
                    Status = b.Status.ToString(),
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = true,
                Data = batteryUnits,
                Message = "Retrieved battery units for station successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving battery units for station {StationId}", stationId);
            return StatusCode(500, new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Tạo pin mới
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BatteryUnitResponseDto>>> CreateBatteryUnit(CreateBatteryUnitDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var existingBattery = await _context.BatteryUnits.FirstOrDefaultAsync(b => b.Serial == dto.Serial);
            if (existingBattery != null)
            {
                return BadRequest(new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Battery with this serial number already exists" });
            }

            var batteryModel = await _context.BatteryModels.FindAsync(dto.BatteryModelId);
            if (batteryModel == null)
            {
                return BadRequest(new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Battery model not found" });
            }

            var station = await _context.Stations.FindAsync(dto.StationId);
            if (station == null)
            {
                return BadRequest(new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Station not found" });
            }

            var batteryUnit = new BatteryUnit
            {
                Serial = dto.Serial,
                BatteryModelId = dto.BatteryModelId,
                StationId = dto.StationId,
                Status = BatteryStatus.Full, // Default to Full/Available
                UpdatedAt = DateTime.UtcNow
            };
            _context.BatteryUnits.Add(batteryUnit);

            if (IsBatteryAvailable(batteryUnit.Status))
            {
                var inventory = await _context.BatteryInventories.FirstOrDefaultAsync(i => i.StationId == dto.StationId && i.BatteryModelId == dto.BatteryModelId);
                if (inventory == null)
                {
                    _context.BatteryInventories.Add(new BatteryInventory
                    {
                        StationId = dto.StationId,
                        BatteryModelId = dto.BatteryModelId,
                        Quantity = 1,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    inventory.Quantity++;
                    inventory.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new BatteryUnitResponseDto
            {
                Id = batteryUnit.Id,
                Serial = batteryUnit.Serial,
                BatteryModelId = batteryUnit.BatteryModelId,
                BatteryModelName = batteryModel.Name,
                Voltage = batteryModel.Voltage,
                CapacityWh = batteryModel.CapacityWh,
                Manufacturer = batteryModel.Manufacturer,
                StationId = batteryUnit.StationId,
                StationName = station.Name,
                Status = batteryUnit.Status.ToString(),
                UpdatedAt = batteryUnit.UpdatedAt
            };

            return CreatedAtAction(nameof(GetBatteryUnit), new { id = batteryUnit.Id }, new ApiResponse<BatteryUnitResponseDto> { Success = true, Data = response, Message = "Battery unit created successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating battery unit");
            return StatusCode(500, new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Thêm nhiều pin vào một trạm
    /// </summary>
    [HttpPost("add-to-station")]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> AddBatteriesToStation(AddBatteriesToStationDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var station = await _context.Stations.FindAsync(dto.StationId);
            if (station == null)
            {
                return BadRequest(new ApiResponse<List<BatteryUnitResponseDto>> { Success = false, Message = "Station not found" });
            }

            var serials = dto.BatteryUnits.Select(b => b.Serial).ToList();
            var existingSerials = await _context.BatteryUnits.Where(b => serials.Contains(b.Serial)).Select(b => b.Serial).ToListAsync();
            if (existingSerials.Any())
            {
                return BadRequest(new ApiResponse<List<BatteryUnitResponseDto>> { Success = false, Message = $"The following serial numbers already exist: {string.Join(", ", existingSerials)}" });
            }

            var batteryModelIds = dto.BatteryUnits.Select(b => b.BatteryModelId).Distinct().ToList();
            var batteryModels = await _context.BatteryModels.Where(bm => batteryModelIds.Contains(bm.Id)).ToDictionaryAsync(bm => bm.Id);
            if (batteryModels.Count != batteryModelIds.Count)
            {
                var missingIds = batteryModelIds.Except(batteryModels.Keys).ToList();
                return BadRequest(new ApiResponse<List<BatteryUnitResponseDto>> { Success = false, Message = $"The following battery model IDs not found: {string.Join(", ", missingIds)}" });
            }

            var batteryUnits = dto.BatteryUnits.Select(b => new BatteryUnit
            {
                Serial = b.Serial,
                BatteryModelId = b.BatteryModelId,
                StationId = dto.StationId,
                Status = BatteryStatus.Full,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            _context.BatteryUnits.AddRange(batteryUnits);

            var inventoryUpdates = batteryUnits.Where(b => IsBatteryAvailable(b.Status)).GroupBy(b => b.BatteryModelId).Select(g => new { BatteryModelId = g.Key, Count = g.Count() });
            foreach (var update in inventoryUpdates)
            {
                var inventory = await _context.BatteryInventories.FirstOrDefaultAsync(i => i.StationId == dto.StationId && i.BatteryModelId == update.BatteryModelId);
                if (inventory == null)
                {
                    _context.BatteryInventories.Add(new BatteryInventory
                    {
                        StationId = dto.StationId,
                        BatteryModelId = update.BatteryModelId,
                        Quantity = update.Count,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    inventory.Quantity += update.Count;
                    inventory.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = batteryUnits.Select(b => new BatteryUnitResponseDto
            {
                Id = b.Id,
                Serial = b.Serial,
                BatteryModelId = b.BatteryModelId,
                BatteryModelName = batteryModels[b.BatteryModelId].Name,
                Voltage = batteryModels[b.BatteryModelId].Voltage,
                CapacityWh = batteryModels[b.BatteryModelId].CapacityWh,
                Manufacturer = batteryModels[b.BatteryModelId].Manufacturer,
                StationId = b.StationId,
                StationName = station.Name,
                Status = b.Status.ToString(),
                UpdatedAt = b.UpdatedAt
            }).ToList();

            return Ok(new ApiResponse<List<BatteryUnitResponseDto>> { Success = true, Data = response, Message = $"Successfully added {batteryUnits.Count} battery units to station" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adding battery units to station");
            return StatusCode(500, new ApiResponse<List<BatteryUnitResponseDto>> { Success = false, Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Lấy thông tin chi tiết một pin
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<BatteryUnitResponseDto>>> GetBatteryUnit(Guid id)
    {
        try
        {
            var batteryUnit = await _context.BatteryUnits.Include(b => b.Model).Include(b => b.Station).FirstOrDefaultAsync(b => b.Id == id);
            if (batteryUnit == null)
            {
                return NotFound(new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Battery unit not found" });
            }

            var response = new BatteryUnitResponseDto
            {
                Id = batteryUnit.Id,
                Serial = batteryUnit.Serial,
                BatteryModelId = batteryUnit.BatteryModelId,
                BatteryModelName = batteryUnit.Model.Name,
                Voltage = batteryUnit.Model.Voltage,
                CapacityWh = batteryUnit.Model.CapacityWh,
                Manufacturer = batteryUnit.Model.Manufacturer,
                StationId = batteryUnit.StationId,
                StationName = batteryUnit.Station?.Name ?? "",
                Status = batteryUnit.Status.ToString(),
                UpdatedAt = batteryUnit.UpdatedAt
            };

            return Ok(new ApiResponse<BatteryUnitResponseDto> { Success = true, Data = response, Message = "Retrieved battery unit successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving battery unit {BatteryId}", id);
            return StatusCode(500, new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Cập nhật trạng thái pin
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponse<BatteryUnitResponseDto>>> UpdateBatteryStatus(Guid id, [FromBody] BatteryStatus status)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var batteryUnit = await _context.BatteryUnits.Include(b => b.Model).Include(b => b.Station).FirstOrDefaultAsync(b => b.Id == id);
            if (batteryUnit == null)
            {
                return NotFound(new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Battery unit not found" });
            }

            var oldStatus = batteryUnit.Status;
            var newStatus = status;

            bool wasAvailable = IsBatteryAvailable(oldStatus);
            bool isAvailable = IsBatteryAvailable(newStatus);

            if (wasAvailable != isAvailable)
            {
                var inventory = await _context.BatteryInventories.FirstOrDefaultAsync(i => i.StationId == batteryUnit.StationId && i.BatteryModelId == batteryUnit.BatteryModelId);
                if (isAvailable)
                {
                    if (inventory == null)
                    {
                        _context.BatteryInventories.Add(new BatteryInventory { StationId = batteryUnit.StationId, BatteryModelId = batteryUnit.BatteryModelId, Quantity = 1, UpdatedAt = DateTime.UtcNow });
                    }
                    else
                    {
                        inventory.Quantity++;
                        inventory.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    if (inventory != null)
                    {
                        inventory.Quantity--;
                        inventory.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            batteryUnit.Status = newStatus;
            batteryUnit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new BatteryUnitResponseDto
            {
                Id = batteryUnit.Id,
                Serial = batteryUnit.Serial,
                BatteryModelId = batteryUnit.BatteryModelId,
                BatteryModelName = batteryUnit.Model.Name,
                Voltage = batteryUnit.Model.Voltage,
                CapacityWh = batteryUnit.Model.CapacityWh,
                Manufacturer = batteryUnit.Model.Manufacturer,
                StationId = batteryUnit.StationId,
                StationName = batteryUnit.Station?.Name ?? "",
                Status = batteryUnit.Status.ToString(),
                UpdatedAt = batteryUnit.UpdatedAt
            };

            return Ok(new ApiResponse<BatteryUnitResponseDto> { Success = true, Data = response, Message = "Battery status updated successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error updating battery status for {BatteryId}", id);
            return StatusCode(500, new ApiResponse<BatteryUnitResponseDto> { Success = false, Message = "Internal server error" });
        }
    }

    /// <summary>
    /// Xóa pin
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBatteryUnit(Guid id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var batteryUnit = await _context.BatteryUnits.FindAsync(id);
            if (batteryUnit == null)
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = "Battery unit not found" });
            }

            if (batteryUnit.Status == BatteryStatus.InUse || batteryUnit.Status == BatteryStatus.Reserved)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Cannot delete a battery that is currently in use or reserved." });
            }

            if (IsBatteryAvailable(batteryUnit.Status))
            {
                var inventory = await _context.BatteryInventories.FirstOrDefaultAsync(i => i.StationId == batteryUnit.StationId && i.BatteryModelId == batteryUnit.BatteryModelId);
                if (inventory != null)
                {
                    inventory.Quantity--;
                    inventory.UpdatedAt = DateTime.UtcNow;
                }
            }

            _context.BatteryUnits.Remove(batteryUnit);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new ApiResponse<object> { Success = true, Message = "Battery unit deleted successfully" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting battery unit {BatteryId}", id);
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Internal server error" });
        }
    }
}
