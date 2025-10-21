namespace EVBSS.Api.Services;

/// <summary>
/// Local file storage service implementation that saves files to wwwroot directory
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IImageWatermarkService _watermarkService;

    public LocalFileStorageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor, IImageWatermarkService watermarkService)
    {
        _env = env;
        _httpContextAccessor = httpContextAccessor;
        _watermarkService = watermarkService;
    }

    public async Task<string> SaveFileAsync(IFormFile file, string subFolder)
    {
        // 1. Tạo tên file duy nhất để tránh trùng lặp
        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

        // 2. Xác định đường dẫn lưu file trong wwwroot
        var uploadsFolderPath = Path.Combine(_env.WebRootPath, "uploads", subFolder);
        if (!Directory.Exists(uploadsFolderPath))
        {
            Directory.CreateDirectory(uploadsFolderPath);
        }
        var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

        // 3. Xử lý file trước khi lưu
        Stream fileStream;
        if (subFolder == "registrations" && IsImageFile(fileExtension))
        {
            // Thêm watermark cho ảnh cà vẹt xe
            Console.WriteLine($"Adding watermark to registration image: {file.FileName}");
            using var originalStream = file.OpenReadStream();
            fileStream = await _watermarkService.AddWatermarkToRegistrationImageAsync(originalStream);
            Console.WriteLine($"Watermark added successfully to: {file.FileName}");
        }
        else
        {
            // File thường, không cần watermark
            Console.WriteLine($"Skipping watermark for file: {file.FileName} in folder: {subFolder}");
            fileStream = file.OpenReadStream();
        }

        // 4. Lưu file vào đường dẫn
        await using (var outputStream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(outputStream);
        }

        // Cleanup
        if (fileStream != file.OpenReadStream())
        {
            fileStream.Dispose();
        }

        // 5. Tạo và trả về URL công khai
        var request = _httpContextAccessor.HttpContext!.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var fileUrl = $"{baseUrl}/uploads/{subFolder}/{uniqueFileName}";

        return fileUrl;
    }

    /// <summary>
    /// Kiểm tra xem file có phải là ảnh không
    /// </summary>
    private static bool IsImageFile(string fileExtension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        return imageExtensions.Contains(fileExtension.ToLowerInvariant());
    }
}
