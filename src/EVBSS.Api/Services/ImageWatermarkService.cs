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
        var fontSize = Math.Max(imageWidth, imageHeight) / 25; // Font size nhỏ hơn để phù hợp với watermark chéo
        var font = new Font("Arial", fontSize, FontStyle.Bold);
        
        // Màu watermark (trắng với độ trong suốt thấp hơn)
        var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)); // 60/255 = ~23% opacity
        
        // Tính toán góc xoay và khoảng cách giữa các watermark
        var textSize = graphics.MeasureString(watermarkText, font);
        var spacing = Math.Max(textSize.Width, textSize.Height) * 1.5f; // Khoảng cách giữa các watermark
        
        // Tính toán số lượng watermark cần thiết để phủ toàn bộ ảnh
        var diagonalLength = Math.Sqrt(imageWidth * imageWidth + imageHeight * imageHeight);
        var numberOfWatermarks = (int)(diagonalLength / spacing) + 2;
        
        // Tạo watermark chéo qua toàn bộ ảnh
        for (int i = 0; i < numberOfWatermarks; i++)
        {
            // Tính toán vị trí cho watermark thứ i
            var x = (float)(i * spacing * Math.Cos(Math.PI / 4)) - textSize.Width / 2;
            var y = (float)(i * spacing * Math.Sin(Math.PI / 4)) - textSize.Height / 2;
            
            // Kiểm tra xem watermark có nằm trong phạm vi ảnh không
            if (x + textSize.Width > 0 && x < imageWidth && y + textSize.Height > 0 && y < imageHeight)
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
        
        // Cleanup
        font.Dispose();
        brush.Dispose();
        
        await Task.CompletedTask; // Để method async
    }
}
