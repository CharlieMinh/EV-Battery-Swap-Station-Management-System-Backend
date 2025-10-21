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
        try
        {
            // Tính toán kích thước font dựa trên kích thước ảnh
            var fontSize = Math.Max(imageWidth, imageHeight) / 10; // Font size rất lớn để test
            var font = new Font("Arial", fontSize, FontStyle.Bold);
            
            // Màu watermark (đỏ đậm để dễ thấy)
            var brush = new SolidBrush(Color.FromArgb(200, 255, 0, 0)); // Đỏ với độ trong suốt 78%
            
            // Tính toán kích thước text
            var textSize = graphics.MeasureString(watermarkText, font);
            
            // Vẽ watermark đơn giản ở giữa ảnh trước
            var centerX = (imageWidth - textSize.Width) / 2;
            var centerY = (imageHeight - textSize.Height) / 2;
            
            // Vẽ watermark ở giữa ảnh
            graphics.DrawString(watermarkText, font, brush, centerX, centerY);
            
            // Vẽ thêm một vài watermark ở các góc
            graphics.DrawString(watermarkText, font, brush, 50, 50); // Góc trên trái
            graphics.DrawString(watermarkText, font, brush, imageWidth - textSize.Width - 50, 50); // Góc trên phải
            graphics.DrawString(watermarkText, font, brush, 50, imageHeight - textSize.Height - 50); // Góc dưới trái
            graphics.DrawString(watermarkText, font, brush, imageWidth - textSize.Width - 50, imageHeight - textSize.Height - 50); // Góc dưới phải
            
            // Cleanup
            font.Dispose();
            brush.Dispose();
            
            Console.WriteLine($"Watermark drawn successfully: {watermarkText} on {imageWidth}x{imageHeight} image");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing watermark: {ex.Message}");
            throw;
        }
        
        await Task.CompletedTask; // Để method async
    }
}
