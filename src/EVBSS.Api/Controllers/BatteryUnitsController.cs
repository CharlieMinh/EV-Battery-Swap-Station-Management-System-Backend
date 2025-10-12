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

    /// <summary>
    /// Lấy danh sách tất cả pin
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
                    IsReserved = b.IsReserved,
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
    /// Lấy danh sách pin theo station ID
    /// </summary>
    [HttpGet("station/{stationId}")]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> GetBatteryUnitsByStation(Guid stationId)
    {
        try
        {
            // Kiểm tra station có tồn tại không
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
                    IsReserved = b.IsReserved,
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
        try
        {
            // Kiểm tra serial đã tồn tại chưa
            var existingBattery = await _context.BatteryUnits
                .FirstOrDefaultAsync(b => b.Serial == dto.Serial);

            if (existingBattery != null)
            {
                return BadRequest(new ApiResponse<BatteryUnitResponseDto>
                {
                    Success = false,
                    Message = "Battery with this serial number already exists"
                });
            }

            // Kiểm tra battery model có tồn tại không
            var batteryModel = await _context.BatteryModels
                .FirstOrDefaultAsync(bm => bm.Id == dto.BatteryModelId);

            if (batteryModel == null)
            {
                return BadRequest(new ApiResponse<BatteryUnitResponseDto>
                {
                    Success = false,
                    Message = "Battery model not found"
                });
            }

            // Kiểm tra station có tồn tại không
            var station = await _context.Stations
                .FirstOrDefaultAsync(s => s.Id == dto.StationId);

            if (station == null)
            {
                return BadRequest(new ApiResponse<BatteryUnitResponseDto>
                {
                    Success = false,
                    Message = "Station not found"
                });
            }

            var batteryUnit = new BatteryUnit
            {
                Serial = dto.Serial,
                BatteryModelId = dto.BatteryModelId,
                StationId = dto.StationId,
                Status = BatteryStatus.Full,
                UpdatedAt = DateTime.UtcNow
            };

            _context.BatteryUnits.Add(batteryUnit);
            await _context.SaveChangesAsync();

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
                IsReserved = batteryUnit.IsReserved,
                UpdatedAt = batteryUnit.UpdatedAt
            };

            _logger.LogInformation("Created battery unit with ID {BatteryId}", batteryUnit.Id);

            return CreatedAtAction(nameof(GetBatteryUnit), new { id = batteryUnit.Id }, 
                new ApiResponse<BatteryUnitResponseDto>
                {
                    Success = true,
                    Data = response,
                    Message = "Battery unit created successfully"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating battery unit");
            return StatusCode(500, new ApiResponse<BatteryUnitResponseDto>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Thêm nhiều pin vào một trạm
    /// </summary>
    [HttpPost("add-to-station")]
    public async Task<ActionResult<ApiResponse<List<BatteryUnitResponseDto>>>> AddBatteriesToStation(AddBatteriesToStationDto dto)
    {
        try
        {
            // Kiểm tra station có tồn tại không
            var station = await _context.Stations
                .FirstOrDefaultAsync(s => s.Id == dto.StationId);

            if (station == null)
            {
                return BadRequest(new ApiResponse<List<BatteryUnitResponseDto>>
                {
                    Success = false,
                    Message = "Station not found"
                });
            }

            // Kiểm tra các serial đã tồn tại chưa
            var serials = dto.BatteryUnits.Select(b => b.Serial).ToList();
            var existingSerials = await _context.BatteryUnits
                .Where(b => serials.Contains(b.Serial))
                .Select(b => b.Serial)
                .ToListAsync();

            if (existingSerials.Any())
            {
                return BadRequest(new ApiResponse<List<BatteryUnitResponseDto>>
                {
                    Success = false,
                    Message = $"The following serial numbers already exist: {string.Join(", ", existingSerials)}"
                });
            }

            // Kiểm tra tất cả battery models có tồn tại không
            var batteryModelIds = dto.BatteryUnits.Select(b => b.BatteryModelId).Distinct().ToList();
            var batteryModels = await _context.BatteryModels
                .Where(bm => batteryModelIds.Contains(bm.Id))
                .ToListAsync();

            if (batteryModels.Count != batteryModelIds.Count)
            {
                var foundIds = batteryModels.Select(bm => bm.Id).ToList();
                var missingIds = batteryModelIds.Except(foundIds).ToList();
                return BadRequest(new ApiResponse<List<BatteryUnitResponseDto>>
                {
                    Success = false,
                    Message = $"The following battery model IDs not found: {string.Join(", ", missingIds)}"
                });
            }

            // Tạo danh sách battery units
            var batteryUnits = dto.BatteryUnits.Select(b => new BatteryUnit
            {
                Serial = b.Serial,
                BatteryModelId = b.BatteryModelId,
                StationId = dto.StationId,
                Status = BatteryStatus.Full,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            _context.BatteryUnits.AddRange(batteryUnits);
            await _context.SaveChangesAsync();

            // Tạo response
            var response = batteryUnits.Select(b =>
            {
                var model = batteryModels.First(bm => bm.Id == b.BatteryModelId);
                return new BatteryUnitResponseDto
                {
                    Id = b.Id,
                    Serial = b.Serial,
                    BatteryModelId = b.BatteryModelId,
                    BatteryModelName = model.Name,
                    Voltage = model.Voltage,
                    CapacityWh = model.CapacityWh,
                    Manufacturer = model.Manufacturer,
                    StationId = b.StationId,
                    StationName = station.Name,
                    Status = b.Status.ToString(),
                    IsReserved = b.IsReserved,
                    UpdatedAt = b.UpdatedAt
                };
            }).ToList();

            _logger.LogInformation("Added {Count} battery units to station {StationId}", 
                batteryUnits.Count, dto.StationId);

            return Ok(new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = true,
                Data = response,
                Message = $"Successfully added {batteryUnits.Count} battery units to station"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding battery units to station");
            return StatusCode(500, new ApiResponse<List<BatteryUnitResponseDto>>
            {
                Success = false,
                Message = "Internal server error"
            });
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
            var batteryUnit = await _context.BatteryUnits
                .Include(b => b.Model)
                .Include(b => b.Station)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batteryUnit == null)
            {
                return NotFound(new ApiResponse<BatteryUnitResponseDto>
                {
                    Success = false,
                    Message = "Battery unit not found"
                });
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
                IsReserved = batteryUnit.IsReserved,
                UpdatedAt = batteryUnit.UpdatedAt
            };

            return Ok(new ApiResponse<BatteryUnitResponseDto>
            {
                Success = true,
                Data = response,
                Message = "Retrieved battery unit successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving battery unit {BatteryId}", id);
            return StatusCode(500, new ApiResponse<BatteryUnitResponseDto>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Cập nhật trạng thái pin
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiResponse<BatteryUnitResponseDto>>> UpdateBatteryStatus(
        Guid id, [FromBody] BatteryStatus status)
    {
        try
        {
            var batteryUnit = await _context.BatteryUnits
                .Include(b => b.Model)
                .Include(b => b.Station)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batteryUnit == null)
            {
                return NotFound(new ApiResponse<BatteryUnitResponseDto>
                {
                    Success = false,
                    Message = "Battery unit not found"
                });
            }

            batteryUnit.Status = status;
            batteryUnit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

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
                IsReserved = batteryUnit.IsReserved,
                UpdatedAt = batteryUnit.UpdatedAt
            };

            _logger.LogInformation("Updated battery unit {BatteryId} status to {Status}", id, status);

            return Ok(new ApiResponse<BatteryUnitResponseDto>
            {
                Success = true,
                Data = response,
                Message = "Battery status updated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating battery status for {BatteryId}", id);
            return StatusCode(500, new ApiResponse<BatteryUnitResponseDto>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Xóa pin
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBatteryUnit(Guid id)
    {
        try
        {
            var batteryUnit = await _context.BatteryUnits.FindAsync(id);

            if (batteryUnit == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Battery unit not found"
                });
            }

            // Kiểm tra pin có đang được sử dụng không
            if (batteryUnit.Status == BatteryStatus.Issued || batteryUnit.IsReserved)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Cannot delete battery unit that is currently issued or reserved"
                });
            }

            _context.BatteryUnits.Remove(batteryUnit);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted battery unit {BatteryId}", id);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Battery unit deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting battery unit {BatteryId}", id);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }
}