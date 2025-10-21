namespace EVBSS.Api.Services;

/// <summary>
/// Service interface for adding watermarks to images
/// </summary>
public interface IImageWatermarkService
{
    /// <summary>
    /// Thêm watermark vào ảnh cà vẹt xe
    /// </summary>
    /// <param name="imageStream">Stream của ảnh gốc</param>
    /// <param name="watermarkText">Text watermark</param>
    /// <returns>Stream của ảnh đã có watermark</returns>
    Task<Stream> AddWatermarkToRegistrationImageAsync(Stream imageStream, string watermarkText = "EV Battery Swap Station");
}
