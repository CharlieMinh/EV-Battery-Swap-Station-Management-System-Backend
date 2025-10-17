namespace EVBSS.Api.Services;

/// <summary>
/// Service interface for handling file storage operations
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Lưu file vào nơi lưu trữ và trả về URL công khai.
    /// </summary>
    /// <param name="file">File được tải lên.</param>
    /// <param name="subFolder">Thư mục con để lưu file (ví dụ: "vehicles", "registrations").</param>
    /// <returns>URL để truy cập file.</returns>
    Task<string> SaveFileAsync(IFormFile file, string subFolder);
}
