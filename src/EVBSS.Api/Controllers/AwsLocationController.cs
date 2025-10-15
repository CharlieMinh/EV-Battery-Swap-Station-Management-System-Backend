using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Threading.Tasks;

// [Phần DTOs giữ nguyên]
// ... (AwsPlaceGeometry, AwsPlace, AwsResult, AwsGeocodeResponse, GeocodeRequest, GeocodeResponse)
// [Để giữ cho file này gọn, tôi không lặp lại phần DTOs]

[Route("api/[controller]")]
[ApiController]
public class AwsLocationController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AwsSettings _awsSettings;

    // Định nghĩa DTOs nội bộ cho phản hồi AWS
    private class AwsPlaceGeometry
    {
        public required double[] Point { get; set; }
    }

    private class AwsPlace
    {
        public required AwsPlaceGeometry Geometry { get; set; }
        public required string Label { get; set; }
    }

    private class AwsResult
    {
        public required AwsPlace Place { get; set; }
    }

    private class AwsGeocodeResponse
    {
        public required AwsResult[] Results { get; set; }
    }

    // Định nghĩa kiểu dữ liệu cho yêu cầu từ Frontend
    public class GeocodeRequest
    {
        // Sử dụng required để tránh cảnh báo Nullability
        [System.ComponentModel.DataAnnotations.Required]
        public required string Address { get; set; }
    }

    // Định nghĩa kiểu dữ liệu cho phản hồi Geocode đơn giản
    public class GeocodeResponse
    {
        public required double Lat { get; set; }
        public required double Lng { get; set; }
        public required string Label { get; set; }
    }


    public AwsLocationController(
        IHttpClientFactory httpClientFactory,
        IOptions<AwsSettings> awsSettingsOptions)
    {
        _httpClientFactory = httpClientFactory;
        _awsSettings = awsSettingsOptions.Value!;
    }

    [HttpPost("geocode")]
    public async Task<IActionResult> Geocode([FromBody] GeocodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { error = "Địa chỉ không hợp lệ." });
        }

        if (string.IsNullOrEmpty(_awsSettings.ApiKey))
        {
            return StatusCode(500, new { error = "Lỗi cấu hình Server: AWS API Key bị thiếu." });
        }

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            var awsBaseUrl = $"https://places.geo.{_awsSettings.Region}.amazonaws.com/places/v0/indexes/{_awsSettings.PlaceIndex}/search/text";

            // --- 1. Vô hiệu hóa hoặc đặt lại User-Agent (Khắc phục lỗi Non-ASCII) ---
            // Đảm bảo User-Agent không chứa ký tự Non-ASCII từ môi trường
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EVBSS-Proxy/1.0");

            // --- 2. Chuẩn bị Payload ---
            var awsPayload = new
            {
                Text = request.Address,
                MaxResults = 1,
                BiasPosition = new[] { 106.7, 10.8 }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(awsPayload),
                Encoding.UTF8,
                "application/json"
            );

            // --- 3. SỬ DỤNG HttpRequestMessage để kiểm soát Header tuyệt đối ---
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, awsBaseUrl);

            // Gán Content (Content Header sẽ được quản lý tại đây)
            httpRequest.Content = jsonContent;

            // Thêm API Key vào Request Header (chắc chắn chỉ có ASCII)
            // LƯU Ý: Không sử dụng DefaultRequestHeaders.Add() cho API Key
            httpRequest.Headers.Add("x-api-key", _awsSettings.ApiKey.Trim());

            // AWS response (Gửi yêu cầu)
            var awsResponse = await httpClient.SendAsync(httpRequest);

            // --- Xử lý Phản hồi ---
            if (!awsResponse.IsSuccessStatusCode)
            {
                var errorContent = await awsResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"AWS Geolocation API failed: {awsResponse.StatusCode}. Content: {errorContent}");
                return StatusCode((int)awsResponse.StatusCode, new { error = "Lỗi từ AWS Geolocation", details = errorContent });
            }

            var content = await awsResponse.Content.ReadAsStringAsync();
            var awsResult = JsonSerializer.Deserialize<AwsGeocodeResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var result = awsResult?.Results?.FirstOrDefault();

            if (result?.Place?.Geometry?.Point != null && result.Place.Geometry.Point.Length >= 2)
            {
                var response = new GeocodeResponse
                {
                    Lng = result.Place.Geometry.Point[0],
                    Lat = result.Place.Geometry.Point[1],
                    Label = result.Place.Label
                };
                return Ok(response);
            }

            return Ok(new { lat = 0.0, lng = 0.0, label = "Không tìm thấy" });

        }
        catch (Exception ex)
        {
            Console.WriteLine($"System Error during Geocoding: {ex}");
            return StatusCode(500, new { error = "Lỗi không xác định khi xử lý Geocoding.", message = ex.Message });
        }
    }
}
