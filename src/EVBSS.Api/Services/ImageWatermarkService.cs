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
            // Sử dụng text "EVBSS" thay vì text mặc định
            var displayText = "EVBSS System";
            
            // Tính toán kích thước font dựa trên kích thước ảnh (lớn hơn để nổi bật)
            var fontSize = Math.Max(imageWidth, imageHeight) / 8; // Font size lớn như code mẫu
            var font = new Font("Arial", fontSize, FontStyle.Italic, GraphicsUnit.Pixel);
            
            // Màu watermark (trắng với độ trong suốt như code mẫu)
            var brush = new SolidBrush(Color.FromArgb(120, 255, 255, 255)); // Trắng mờ (độ trong suốt 120)
            
            // Đo kích thước text để căn giữa
            var textSize = graphics.MeasureString(displayText, font);
            var x = (imageWidth - textSize.Width) / 2;
            var y = (imageHeight - textSize.Height) / 2;
            
            // Xoay nhẹ 10 độ cho watermark nghiêng (như code mẫu)
            graphics.TranslateTransform(x + textSize.Width / 2, y + textSize.Height / 2);
            graphics.RotateTransform(-10);
            graphics.TranslateTransform(-(x + textSize.Width / 2), -(y + textSize.Height / 2));
            
            // Vẽ text ở giữa ảnh
            graphics.DrawString(displayText, font, brush, x, y);
            
            // Reset transform
            graphics.ResetTransform();
            
            // Cleanup
            font.Dispose();
            brush.Dispose();
            
            Console.WriteLine($"Watermark drawn successfully: {displayText} on {imageWidth}x{imageHeight} image");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing watermark: {ex.Message}");
            throw;
        }
        
        await Task.CompletedTask; // Để method async
    }
}
