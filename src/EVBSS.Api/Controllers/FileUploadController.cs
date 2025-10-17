using Microsoft.AspNetCore.Mvc;
using EVBSS.Api.Services;
using EVBSS.Api.Data;
using Microsoft.AspNetCore.Authorization;

namespace EVBSS.Api.Controllers;

/// <summary>
/// Controller xử lý upload file ảnh và trả về URL công khai
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Bật lại authorization
public class FileUploadController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(IFileStorageService fileStorageService, ILogger<FileUploadController> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Upload ảnh xe và trả về URL công khai
    /// </summary>
    /// <param name="file">File ảnh xe</param>
    /// <returns>URL để truy cập ảnh</returns>
    [HttpPost("vehicle-photo")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadVehiclePhoto(IFormFile file)
    {
        try
        {
            // Validate file
            var validationResult = ValidateImageFile(file);
            if (validationResult != null)
                return BadRequest(validationResult);

            // Upload file và lấy URL
            var photoUrl = await _fileStorageService.SaveFileAsync(file, "vehicles");

            _logger.LogInformation("Vehicle photo uploaded successfully: {PhotoUrl}", photoUrl);

            return Ok(new FileUploadResponse
            {
                Success = true,
                FileUrl = photoUrl,
                FileName = file.FileName,
                FileSize = file.Length,
                Message = "Ảnh xe đã được tải lên thành công"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading vehicle photo");
            return StatusCode(StatusCodes.Status500InternalServerError, new FileUploadResponse
            {
                Success = false,
                Message = $"Lỗi khi tải lên ảnh xe: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Upload ảnh đăng ký xe (cà vẹt) và trả về URL công khai
    /// </summary>
    /// <param name="file">File ảnh đăng ký xe</param>
    /// <returns>URL để truy cập ảnh</returns>
    [HttpPost("registration-photo")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadRegistrationPhoto(IFormFile file)
    {
        try
        {
            // Validate file
            var validationResult = ValidateImageFile(file);
            if (validationResult != null)
                return BadRequest(validationResult);

            // Upload file và lấy URL
            var photoUrl = await _fileStorageService.SaveFileAsync(file, "registrations");

            _logger.LogInformation("Registration photo uploaded successfully: {PhotoUrl}", photoUrl);

            return Ok(new FileUploadResponse
            {
                Success = true,
                FileUrl = photoUrl,
                FileName = file.FileName,
                FileSize = file.Length,
                Message = "Ảnh đăng ký xe đã được tải lên thành công"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading registration photo");
            return StatusCode(StatusCodes.Status500InternalServerError, new FileUploadResponse
            {
                Success = false,
                Message = $"Lỗi khi tải lên ảnh đăng ký xe: {ex.Message}"
            });
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

/// <summary>
/// Response model cho file upload
/// </summary>
public class FileUploadResponse
{
    public bool Success { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string Message { get; set; } = string.Empty;
}
