using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace EVBSS.Api.Services;

/// <summary>
/// Service implementation for adding watermarks to images
/// </summary>
[SupportedOSPlatform("windows")]
public class ImageWatermarkService : IImageWatermarkService
{
    private readonly ILogger<ImageWatermarkService> _logger;

    public ImageWatermarkService(ILogger<ImageWatermarkService> logger)
    {
        _logger = logger;
    }

    public async Task<Stream> AddWatermarkToRegistrationImageAsync(Stream imageStream, string watermarkText = "EV Battery Swap Station")
    {
        try
        {
            // Đọc ảnh gốc
            using var originalImage = Image.FromStream(imageStream);
            
            // Tạo ảnh mới với cùng kích thước
            using var watermarkedImage = new Bitmap(originalImage.Width, originalImage.Height);
            using var graphics = Graphics.FromImage(watermarkedImage);
            
            // Thiết lập chất lượng vẽ
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            // Vẽ ảnh gốc lên ảnh mới
            graphics.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);
            
            // Thêm watermark
            await AddTextWatermarkAsync(graphics, watermarkText, originalImage.Width, originalImage.Height);
            
            // Chuyển đổi thành stream
            var resultStream = new MemoryStream();
            watermarkedImage.Save(resultStream, ImageFormat.Jpeg);
            resultStream.Position = 0;
            
            _logger.LogInformation("Watermark added successfully to registration image");
            
            return resultStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding watermark to registration image");
            throw new InvalidOperationException($"Failed to add watermark: {ex.Message}", ex);
        }
    }

    private async Task AddTextWatermarkAsync(Graphics graphics, string watermarkText, int imageWidth, int imageHeight)
    {
        // Tính toán kích thước font dựa trên kích thước ảnh
        var fontSize = Math.Max(imageWidth, imageHeight) / 20; // Font size tỷ lệ với ảnh
        var font = new Font("Arial", fontSize, FontStyle.Bold);
        
        // Màu watermark (trắng với độ trong suốt)
        var brush = new SolidBrush(Color.FromArgb(120, 255, 255, 255)); // 120/255 = ~47% opacity
        
        // Tính toán vị trí watermark (góc dưới bên phải)
        var textSize = graphics.MeasureString(watermarkText, font);
        var x = imageWidth - textSize.Width - 20; // Cách lề phải 20px
        var y = imageHeight - textSize.Height - 20; // Cách lề dưới 20px
        
        // Thêm shadow effect
        var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
        graphics.DrawString(watermarkText, font, shadowBrush, x + 2, y + 2);
        
        // Vẽ watermark chính
        graphics.DrawString(watermarkText, font, brush, x, y);
        
        // Cleanup
        font.Dispose();
        brush.Dispose();
        shadowBrush.Dispose();
        
        await Task.CompletedTask; // Để method async
    }
}
