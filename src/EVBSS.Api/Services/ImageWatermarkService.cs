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
        var fontSize = Math.Max(imageWidth, imageHeight) / 15; // Font size lớn hơn để dễ thấy
        var font = new Font("Arial", fontSize, FontStyle.Bold);
        
        // Màu watermark (đỏ với độ trong suốt để dễ thấy)
        var brush = new SolidBrush(Color.FromArgb(150, 255, 0, 0)); // Đỏ với độ trong suốt 59%
        
        // Tính toán kích thước text
        var textSize = graphics.MeasureString(watermarkText, font);
        
        // Tính toán khoảng cách giữa các watermark
        var spacingX = textSize.Width * 2f; // Khoảng cách lớn hơn
        var spacingY = textSize.Height * 2f;
        
        // Tính toán số lượng watermark theo chiều ngang và dọc
        var numberOfWatermarksX = (int)(imageWidth / spacingX) + 1;
        var numberOfWatermarksY = (int)(imageHeight / spacingY) + 1;
        
        // Tạo watermark chéo qua toàn bộ ảnh
        for (int row = 0; row < numberOfWatermarksY; row++)
        {
            for (int col = 0; col < numberOfWatermarksX; col++)
            {
                // Tính toán vị trí cho watermark
                var x = col * spacingX;
                var y = row * spacingY;
                
                // Kiểm tra xem watermark có nằm trong phạm vi ảnh không
                if (x + textSize.Width < imageWidth && y + textSize.Height < imageHeight)
                {
                    // Lưu trạng thái graphics hiện tại
                    var state = graphics.Save();
                    
                    // Xoay graphics để tạo watermark chéo
                    graphics.RotateTransform(-45f); // Xoay -45 độ
                    
                    // Vẽ watermark
                    graphics.DrawString(watermarkText, font, brush, x, y);
                    
                    // Khôi phục trạng thái graphics
                    graphics.Restore(state);
                }
            }
        }
        
        // Cleanup
        font.Dispose();
        brush.Dispose();
        
        await Task.CompletedTask; // Để method async
    }
}
