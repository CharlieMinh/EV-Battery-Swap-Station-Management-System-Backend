namespace EVBSS.Api.Services;

/// <summary>
/// Local file storage service implementation that saves files to wwwroot directory
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LocalFileStorageService(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
    {
        _env = env;
        _httpContextAccessor = httpContextAccessor;
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

        // 3. Lưu file vào đường dẫn
        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 4. Tạo và trả về URL công khai
        var request = _httpContextAccessor.HttpContext!.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var fileUrl = $"{baseUrl}/uploads/{subFolder}/{uniqueFileName}";

        return fileUrl;
    }
}
