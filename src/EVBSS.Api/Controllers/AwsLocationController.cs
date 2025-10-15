using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

// Lớp POCO đại diện cho cấu hình AWS của bạn (cần đảm bảo tên namespace khớp)
// Ví dụ: using YourProject.Models; 

[Route("api/[controller]")]
[ApiController]
public class AwsLocationController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly AwsSettings _awsSettings;
    private readonly string _awsBaseUrl;

    public AwsLocationController(
        HttpClient httpClient,
        IOptions<AwsSettings> awsSettingsOptions)
    {
        _httpClient = httpClient;
        _awsSettings = awsSettingsOptions.Value;

        // Xây dựng URL cơ sở từ cấu hình
        _awsBaseUrl = $"https://places.geo.{_awsSettings.Region}.amazonaws.com/places/v0/indexes/{_awsSettings.PlaceIndex}/search/text";
    }

    // Định nghĩa kiểu dữ liệu cho yêu cầu từ Frontend
    public class GeocodeRequest
    {
        public string Address { get; set; }
    }

    // Định nghĩa kiểu dữ liệu cho phản hồi Geocode đơn giản
    public class GeocodeResponse
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Label { get; set; }
    }

    /// <summary>
    /// Endpoint Proxy an toàn để Geocode địa chỉ.
    /// Frontend gọi endpoint này, Backend sẽ chèn API Key và gọi AWS.
    /// </summary>
    [HttpPost("geocode")]
    public async Task<IActionResult> Geocode([FromBody] GeocodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { error = "Địa chỉ không hợp lệ." });
        }

        if (string.IsNullOrEmpty(_awsSettings.ApiKey))
        {
            // Kiểm tra bảo mật
            return StatusCode(500, new { error = "Lỗi cấu hình Server: AWS API Key bị thiếu." });
        }

        try
        {
            // --- 1. Chuẩn bị yêu cầu gửi đến AWS ---
            var awsPayload = new
            {
                Text = request.Address,
                MaxResults = 1,
                BiasPosition = new[] { 106.7, 10.8 } // Tọa độ bias TP.HCM
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(awsPayload),
                Encoding.UTF8,
                "application/json"
            );

            // --- 2. Gửi yêu cầu đến AWS và chèn API Key ---
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _awsSettings.ApiKey);

            var awsResponse = await _httpClient.PostAsync(_awsBaseUrl, jsonContent);

            // --- 3. Xử lý phản hồi từ AWS ---
            if (!awsResponse.IsSuccessStatusCode)
            {
                var errorContent = await awsResponse.Content.ReadAsStringAsync();
                // Log và trả về lỗi AWS
                return StatusCode((int)awsResponse.StatusCode, new { error = "Lỗi từ AWS Geolocation", details = errorContent });
            }

            var content = await awsResponse.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            var results = document.RootElement.GetProperty("Results");

            if (results.GetArrayLength() > 0)
            {
                var place = results[0].GetProperty("Place");
                var point = place.GetProperty("Geometry").GetProperty("Point");

                var response = new GeocodeResponse
                {
                    Lng = point[0].GetDouble(),
                    Lat = point[1].GetDouble(),
                    Label = place.GetProperty("Label").GetString()
                };

                // Trả về kết quả cho Frontend
                return Ok(response);
            }

            // Không tìm thấy kết quả
            return Ok(new { coords = (object)null, label = (object)null });

        }
        catch (Exception ex)
        {
            // Xử lý lỗi hệ thống/network
            return StatusCode(500, new { error = "Lỗi không xác định khi xử lý Geocoding.", message = ex.Message });
        }
    }
}
