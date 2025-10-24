using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Vehicles;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // yêu cầu JWT (Driver)
public class VehiclesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAwsRekognitionService _rekognitionService;
    private readonly IFileStorageService _fileStorageService;
    
    public VehiclesController(AppDbContext db, IAwsRekognitionService rekognitionService, IFileStorageService fileStorageService)
    {
        _db = db;
        _rekognitionService = rekognitionService;
        _fileStorageService = fileStorageService;
    }

    // Lấy userId từ token (sub hoặc NameIdentifier)
    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out userId);
    }

    /// GET /api/v1/vehicles (xe của tôi)
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var items = await _db.Vehicles.AsNoTracking()
            .Include(v => v.VehicleModel)
            .Include(v => v.CompatibleModel)
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VehicleDto(
                v.Id, v.VIN, v.Plate,
                v.VehicleModelId, v.VehicleModel != null ? v.VehicleModel.Name : null, 
                v.VehicleModel != null ? v.VehicleModel.FullName : null, 
                v.VehicleModel != null ? v.VehicleModel.Brand : null,
                v.CompatibleBatteryModelId, v.CompatibleModel.Name,
                v.PhotoUrl, v.RegistrationPhotoUrl, v.CreatedAt, v.UpdatedAt))
            .ToListAsync();

        return Ok(items);
    }

    /// GET /api/v1/vehicles/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var v = await _db.Vehicles.AsNoTracking()
            .Include(x => x.VehicleModel)
            .Include(x => x.CompatibleModel)
            .Where(x => x.Id == id && x.UserId == userId)
            .Select(x => new VehicleDto(
                x.Id, x.VIN, x.Plate,
                x.VehicleModelId, x.VehicleModel != null ? x.VehicleModel.Name : null,
                x.VehicleModel != null ? x.VehicleModel.FullName : null,
                x.VehicleModel != null ? x.VehicleModel.Brand : null,
                x.CompatibleBatteryModelId, x.CompatibleModel.Name,
                x.PhotoUrl, x.RegistrationPhotoUrl, x.CreatedAt, x.UpdatedAt))
            .FirstOrDefaultAsync();

        return v is null
            ? NotFound(new { error = new { code = "VEHICLE_NOT_FOUND", message = "Vehicle not found" } })
            : Ok(v);
    }

    /// POST /api/v1/vehicles
    [HttpPost]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromForm] CreateVehicleRequest req)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var vin = req.Vin.Trim().ToUpperInvariant();
        var plate = req.Plate.Trim().ToUpperInvariant();

        // VehicleModel tồn tại và active?
        var vehicleModel = await _db.VehicleModels
            .Include(vm => vm.CompatibleBatteryModel)
            .FirstOrDefaultAsync(vm => vm.Id == req.VehicleModelId);
        
        if (vehicleModel is null)
            return BadRequest(new { error = new { code = "VEHICLE_MODEL_NOT_FOUND", message = "Vehicle model not found." } });

        if (!vehicleModel.IsActive)
            return BadRequest(new { error = new { code = "VEHICLE_MODEL_INACTIVE", message = "This vehicle model is not supported for battery swap service." } });

        // VIN và Plate phải là duy nhất trên toàn hệ thống
        if (await _db.Vehicles.AnyAsync(v => v.VIN == vin))
            return Conflict(new { error = new { code = "VIN_EXISTS", message = "VIN already exists in the system." } });

        if (await _db.Vehicles.AnyAsync(v => v.Plate == plate))
            return Conflict(new { error = new { code = "PLATE_EXISTS", message = "Plate already exists in the system." } });

        // Upload file ảnh xe
        string photoUrl;
        try
        {
            photoUrl = await _fileStorageService.SaveFileAsync(req.Photo, "vehicles");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = new { code = "FILE_SAVE_ERROR", message = $"Error saving vehicle photo: {ex.Message}" } });
        }

        // Upload file ảnh đăng ký xe
        string registrationPhotoUrl;
        try
        {
            registrationPhotoUrl = await _fileStorageService.SaveFileAsync(req.RegistrationPhoto, "registrations");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = new { code = "FILE_SAVE_ERROR", message = $"Error saving registration photo: {ex.Message}" } });
        }

        var entity = new Vehicle
        {
            UserId = userId,
            VIN = vin,
            Plate = plate,
            VehicleModelId = vehicleModel.Id,
            CompatibleBatteryModelId = vehicleModel.CompatibleBatteryModelId,
            PhotoUrl = photoUrl,
            RegistrationPhotoUrl = registrationPhotoUrl
        };

        _db.Vehicles.Add(entity);
        await _db.SaveChangesAsync();

        var dto = new VehicleDto(
            entity.Id, entity.VIN, entity.Plate,
            entity.VehicleModelId, vehicleModel.Name, vehicleModel.FullName, vehicleModel.Brand,
            entity.CompatibleBatteryModelId, vehicleModel.CompatibleBatteryModel.Name,
            entity.PhotoUrl, entity.RegistrationPhotoUrl, entity.CreatedAt, entity.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    /// POST /api/v1/vehicles/with-url
    /// Tạo xe mới với URL ảnh (sau khi đã upload riêng)
    [HttpPost("with-url")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateWithUrl([FromBody] CreateVehicleWithUrlRequest req)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var vin = req.Vin.Trim().ToUpperInvariant();
        var plate = req.Plate.Trim().ToUpperInvariant();

        // VehicleModel tồn tại và active?
        var vehicleModel = await _db.VehicleModels
            .Include(vm => vm.CompatibleBatteryModel)
            .FirstOrDefaultAsync(vm => vm.Id == req.VehicleModelId);
        
        if (vehicleModel is null)
            return BadRequest(new { error = new { code = "VEHICLE_MODEL_NOT_FOUND", message = "Vehicle model not found." } });

        if (!vehicleModel.IsActive)
            return BadRequest(new { error = new { code = "VEHICLE_MODEL_INACTIVE", message = "This vehicle model is not supported for battery swap service." } });

        // Kiểm tra trùng lặp trên toàn hệ thống
        if (await _db.Vehicles.AnyAsync(v => v.VIN == vin))
            return Conflict(new { error = new { code = "VIN_EXISTS", message = "VIN already exists in the system." } });

        if (await _db.Vehicles.AnyAsync(v => v.Plate == plate))
            return Conflict(new { error = new { code = "PLATE_EXISTS", message = "Plate already exists in the system." } });

        var entity = new Vehicle
        {
            UserId = userId,
            VIN = vin,
            Plate = plate,
            VehicleModelId = vehicleModel.Id,
            CompatibleBatteryModelId = vehicleModel.CompatibleBatteryModelId,
            PhotoUrl = req.PhotoUrl,
            RegistrationPhotoUrl = req.RegistrationPhotoUrl
        };

        _db.Vehicles.Add(entity);
        await _db.SaveChangesAsync();

        var dto = new VehicleDto(
            entity.Id, entity.VIN, entity.Plate,
            entity.VehicleModelId, vehicleModel.Name, vehicleModel.FullName, vehicleModel.Brand,
            entity.CompatibleBatteryModelId, vehicleModel.CompatibleBatteryModel.Name,
            entity.PhotoUrl, entity.RegistrationPhotoUrl, entity.CreatedAt, entity.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    /// PUT /api/v1/vehicles/{id}
    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdateVehicleRequest req)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var vehicle = await _db.Vehicles
            .Include(v => v.VehicleModel)
                .ThenInclude(vm => vm.CompatibleBatteryModel)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (vehicle is null)
            return NotFound(new { error = new { code = "VEHICLE_NOT_FOUND", message = "Vehicle not found" } });

        // Update Plate nếu có
        if (!string.IsNullOrWhiteSpace(req.Plate))
        {
            var plate = req.Plate.Trim().ToUpperInvariant();
            
            // Plate phải là duy nhất trên toàn hệ thống
            if (plate != vehicle.Plate && await _db.Vehicles.AnyAsync(v => v.Plate == plate && v.Id != id))
                return Conflict(new { error = new { code = "PLATE_EXISTS", message = "Plate already exists in the system." } });

            vehicle.Plate = plate;
        }

        // Update PhotoUrl nếu có file ảnh mới
        if (req.Photo != null)
        {
            try
            {
                // Validate file
                var validationResult = ValidateImageFile(req.Photo);
                if (validationResult != null)
                    return BadRequest(validationResult);

                // Upload file và lấy URL
                var photoUrl = await _fileStorageService.SaveFileAsync(req.Photo, "vehicles");
                vehicle.PhotoUrl = photoUrl;
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = new { code = "FILE_SAVE_ERROR", message = $"Error saving vehicle photo: {ex.Message}" } });
            }
        }
        
        // Update RegistrationPhotoUrl nếu có file ảnh đăng ký mới
        if (req.RegistrationPhoto != null)
        {
            try
            {
                // Validate file
                var validationResult = ValidateImageFile(req.RegistrationPhoto);
                if (validationResult != null)
                    return BadRequest(validationResult);

                // Upload file và lấy URL
                var registrationPhotoUrl = await _fileStorageService.SaveFileAsync(req.RegistrationPhoto, "registrations");
                vehicle.RegistrationPhotoUrl = registrationPhotoUrl;
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = new { code = "FILE_SAVE_ERROR", message = $"Error saving registration photo: {ex.Message}" } });
            }
        }

        vehicle.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var dto = new VehicleDto(
            vehicle.Id, vehicle.VIN, vehicle.Plate,
            vehicle.VehicleModelId, vehicle.VehicleModel.Name, vehicle.VehicleModel.FullName, vehicle.VehicleModel.Brand,
            vehicle.CompatibleBatteryModelId, vehicle.VehicleModel.CompatibleBatteryModel.Name,
            vehicle.PhotoUrl, vehicle.RegistrationPhotoUrl, vehicle.CreatedAt, vehicle.UpdatedAt);

        return Ok(dto);
    }

    /// DELETE /api/v1/vehicles/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (v is null)
            return NotFound(new { error = new { code = "VEHICLE_NOT_FOUND", message = "Vehicle not found" } });

        // Kiểm tra xe có subscription nào không (kể cả inactive vì còn FK constraint)
        var hasAnySubscription = await _db.UserSubscriptions
            .AnyAsync(s => s.VehicleId == id);
        
        if (hasAnySubscription)
        {
            var activeCount = await _db.UserSubscriptions.CountAsync(s => s.VehicleId == id && s.IsActive);
            var inactiveCount = await _db.UserSubscriptions.CountAsync(s => s.VehicleId == id && !s.IsActive);
            
            return Conflict(new 
            { 
                error = new 
                { 
                    code = "VEHICLE_HAS_SUBSCRIPTION", 
                    message = $"Cannot delete vehicle. It has {activeCount} active and {inactiveCount} inactive subscription(s). Please contact admin to remove subscriptions first.",
                    details = new 
                    {
                        activeSubscriptions = activeCount,
                        inactiveSubscriptions = inactiveCount,
                        solution = "Ask admin to delete subscription records or set FK constraint to SET NULL"
                    }
                } 
            });
        }

        _db.Vehicles.Remove(v);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Scan vehicle registration image using AWS Rekognition
    /// </summary>
    /// <param name="imageFile">Image file of vehicle registration</param>
    /// <returns>Extracted vehicle data</returns>
    [HttpPost("scan-registration")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(VehicleRegistrationScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ScanRegistration(IFormFile imageFile)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { error = new { code = "INVALID_FILE", message = "Please provide a valid image file." } });

        // Kiểm tra file type
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
        if (!allowedTypes.Contains(imageFile.ContentType.ToLower()))
            return BadRequest(new { error = new { code = "INVALID_FILE_TYPE", message = "Only JPEG and PNG images are allowed." } });

        // Kiểm tra file size (max 10MB)
        if (imageFile.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = new { code = "FILE_TOO_LARGE", message = "Image file size must not exceed 10MB." } });

        try
        {
            using var stream = imageFile.OpenReadStream();
            var result = await _rekognitionService.ScanVehicleRegistrationAsync(stream);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = new { code = "SCAN_FAILED", message = $"Failed to scan registration: {ex.Message}" } });
        }
    }

    /// <summary>
    /// Scan vehicle registration image from URL using AWS Rekognition
    /// </summary>
    /// <param name="request">Request containing image URL</param>
    /// <returns>Extracted vehicle data</returns>
    [HttpPost("scan-registration-url")]
    [ProducesResponseType(typeof(VehicleRegistrationScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ScanRegistrationFromUrl([FromBody] ScanRegistrationUrlRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            return BadRequest(new { error = new { code = "INVALID_URL", message = "Please provide a valid image URL." } });

        try
        {
            var result = await _rekognitionService.ScanVehicleRegistrationFromUrlAsync(request.ImageUrl);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = new { code = "SCAN_FAILED", message = $"Failed to scan registration from URL: {ex.Message}" } });
        }
    }

    /// <summary>
    /// Validate file ảnh
    /// </summary>
    private FileUploadResponse? ValidateImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return new FileUploadResponse
            {
                Success = false,
                Message = "Vui lòng chọn file ảnh"
            };
        }

        // Kiểm tra file type
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
        {
            return new FileUploadResponse
            {
                Success = false,
                Message = "Chỉ chấp nhận file ảnh định dạng JPEG hoặc PNG"
            };
        }

        // Kiểm tra file size (max 10MB)
        if (file.Length > 10 * 1024 * 1024)
        {
            return new FileUploadResponse
            {
                Success = false,
                Message = "Kích thước file không được vượt quá 10MB"
            };
        }

        return null; // Valid
    }
}
