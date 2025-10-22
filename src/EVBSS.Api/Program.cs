using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EVBSS.Api.Data;
using Microsoft.OpenApi.Models;
using EVBSS.Api.Models;     // Role, User
using BCrypt.Net;           // Hash mật khẩu
using EVBSS.Api.Services;   // Services
using EVBSS.Api.Configuration; // VnPayConfig
using Amazon.Rekognition;   // AWS Rekognition
using Amazon.Runtime;       // AWS Credentials

var builder = WebApplication.CreateBuilder(args);

// Swagger (đơn giản)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EVBSS API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập: Bearer {token}"
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = new string[] { }
    });
});

// CORS cho React và Swagger UI
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("frontend", p => p
        .WithOrigins(
            "http://localhost:3000", 
            "http://localhost:5173", 
            "http://127.0.0.1:5173",
            "http://localhost:5194",
            "https://localhost:7240")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); // cho phép gửi cookie
});

// EF Core DbContext
var conn = builder.Configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(conn));

// VNPay Configuration
builder.Services.Configure<VnPayConfig>(builder.Configuration.GetSection("VnPay"));

// JWT (đủ dùng)
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]
                   ?? throw new InvalidOperationException("Missing Jwt:Key")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero //tránh lệch giờ server-client
        };

        // Lấy token từ Cookie "jwt"
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.ContainsKey("jwt"))
                {
                    context.Token = context.Request.Cookies["jwt"];
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();


// Services
builder.Services.AddScoped<SlotReservationService>(); // New slot-based service
builder.Services.AddScoped<ReservationService>(); // Legacy wrapper service
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
// ✅ INVOICE SERVICE REMOVED: Using simplified Payment model
builder.Services.AddScoped<SwapTransactionService>();
builder.Services.AddScoped<IEmailService, EmailService>(); // Email service for OTP
builder.Services.AddScoped<PasswordResetService>(); // Password reset service for Auth
builder.Services.AddScoped<GoogleAuthService>(); // Google OAuth service
builder.Services.AddScoped<StationService>(); // Station management with DisplayId generation
builder.Services.AddScoped<IBatteryInventoryService, BatteryInventoryService>(); // HYBRID SOLUTION: Quantity-based inventory management

// File Storage Service
builder.Services.AddHttpContextAccessor(); // Cần thiết để lấy base URL
builder.Services.AddScoped<IImageWatermarkService, ImageWatermarkService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// AWS Rekognition Service
var awsConfig = builder.Configuration.GetSection("AWS");
var awsAccessKey = awsConfig["AccessKey"];
var awsSecretKey = awsConfig["SecretKey"];
var awsRegion = awsConfig["Region"] ?? "ap-southeast-1";

if (!string.IsNullOrWhiteSpace(awsAccessKey) && !string.IsNullOrWhiteSpace(awsSecretKey))
{
    var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
    builder.Services.AddSingleton<IAmazonRekognition>(_ => 
        new AmazonRekognitionClient(awsCredentials, Amazon.RegionEndpoint.GetBySystemName(awsRegion)));
}
else
{
    // Fallback to default credentials chain (IAM role, environment variables, etc.)
    builder.Services.AddSingleton<IAmazonRekognition>(_ => 
        new AmazonRekognitionClient(Amazon.RegionEndpoint.GetBySystemName(awsRegion)));
}

builder.Services.AddHttpClient<IAwsRekognitionService, AwsRekognitionService>();
builder.Services.AddScoped<IAwsRekognitionService, AwsRekognitionService>();

// Background Services
// Legacy ReservationExpireHostedService removed - using SlotReservationBackgroundService instead
builder.Services.AddHostedService<EVBSS.Api.Services.SlotReservationBackgroundService>(); // New slot-based


var app = builder.Build();

// (Dev) auto-migrate nếu cần
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // ========== SEED STATIONS (TP.HCM) ==========
    // Seed 2 stations in Ho Chi Minh City if table is empty
    if (!db.Stations.Any())
    {
        db.Stations.AddRange(
            new Station
            {
                Id = Guid.NewGuid(),
                DisplayId = null, // Will be auto-generated by StationService
                Name = "Trạm Đổi Pin Quận 1 - Nguyễn Huệ",
                Address = "123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1",
                City = "Hồ Chí Minh",
                Lat = 10.7769,      // Coordinates for Nguyen Hue Walking Street area
                Lng = 106.7009,
                IsActive = true,
                OpenTime = new TimeSpan(8, 0, 0),   // 8:00 AM - Early for commuters
                CloseTime = new TimeSpan(18, 0, 0), // 6:00 PM - Late for night shift
                PhoneNumber = "028-3822-9999",
                PrimaryImageUrl = "https://example.com/stations/q1-nguyen-hue.jpg"
            },
            new Station
            {
                Id = Guid.NewGuid(),
                DisplayId = null, // Will be auto-generated by StationService
                Name = "Trạm Đổi Pin Quận 7 - Phú Mỹ Hưng",
                Address = "456 Đường Nguyễn Văn Linh, Phường Tân Phú, Quận 7",
                City = "Hồ Chí Minh",
                Lat = 10.7329,      // Coordinates for Phu My Hung area
                Lng = 106.7172,
                IsActive = true,
                OpenTime = new TimeSpan(8, 0, 0),   // 8:00 AM
                CloseTime = new TimeSpan(18, 0, 0), // 6:00 PM
                PhoneNumber = "028-5412-8888",
                PrimaryImageUrl = "https://example.com/stations/q7-phu-my-hung.jpg"
            }
        );
        await EVBSS.Api.Data.VehicleModelSeeder.SeedVehicleModelsAsync(db);
        // ...đã xoá seed BatteryUnits lỗi context/models/vf3/vf5/vf8/vf9...
        // ...đã xoá seed BatteryInventories và logger...
        db.SaveChanges();
    }

    // Seed Battery Units moved after VehicleModelSeeder (see line ~370)

    // Seed VinFast-based Subscription Plans
    // Xóa toàn bộ dữ liệu cũ trong bảng SubscriptionPlans
    if (!db.SubscriptionPlans.Any())
    {
        var batteryModels = db.BatteryModels.ToList();
        var allPlans = new List<SubscriptionPlan>();
        foreach (var battery in batteryModels)
        {
            var pinName = battery.Name;
            if (pinName.Contains("VF9"))
            {
                allPlans.AddRange(new[]
                {
                    new SubscriptionPlan
                    {
                        Name = $"Gói Basic - 10 lần/tháng ({pinName})",
                        Description = "Đối tượng: Người dùng có lộ trình di chuyển cố định, chủ yếu trong thành phố.",
                        MonthlyPrice = 4100000m,
                        MaxSwapsPerMonth = 10,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Giá khởi điểm tốt nhất để trải nghiệm dịch vụ.\n✓ Hỗ trợ tiêu chuẩn 24/7 qua tổng đài.\n✓ Linh hoạt nâng cấp gói bất kỳ lúc nào.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Standard - 20 lần/tháng ({pinName})",
                        Description = "Đối tượng: Gia đình, chuyên gia thường xuyên di chuyển giữa các tỉnh hoặc đi nghỉ cuối tuần.",
                        MonthlyPrice = 6500000m,
                        MaxSwapsPerMonth = 20,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Tiết kiệm ~20% trên mỗi lần đổi so với gói Basic.\n✓ Xem lịch sử đổi pin chi tiết trên ứng dụng để quản lý hành trình.\n✓ Tùy chọn tạm ngưng gói 1 lần/năm (tối đa 30 ngày khi không sử dụng xe).",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Premium - Không giới hạn ({pinName})",
                        Description = "Đối tượng: Doanh nhân, chủ doanh nghiệp, người yêu cầu dịch vụ đẳng cấp nhất.",
                        MonthlyPrice = 8100000m,
                        MaxSwapsPerMonth = null,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Đường dây nóng VIP 24/7 (Kết nối trực tiếp chuyên viên cấp cao).\n✓ Ưu tiên đặt trước pin tại trạm (Đảm bảo có pin, không cần chờ).",
                        BatteryModelId = battery.Id
                    }
                });
            }
            else if (pinName.Contains("VF8"))
            {
                allPlans.AddRange(new[]
                {
                    new SubscriptionPlan
                    {
                        Name = $"Gói Basic - 10 lần/tháng ({pinName})",
                        Description = "Đối tượng: Người đi làm hàng ngày, nhu cầu di chuyển cơ bản.",
                        MonthlyPrice = 2200000m,
                        MaxSwapsPerMonth = 10,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Chi phí tối ưu cho nhu cầu đi lại cố định.\n✓ Hỗ trợ 24/7 qua ứng dụng và tổng đài.\n✓ Dễ dàng nâng cấp khi phát sinh nhu cầu di chuyển nhiều hơn.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Standard - 20 lần/tháng ({pinName})",
                        Description = "Đối tượng: Cấp quản lý, người thường xuyên công tác, di chuyển liên tỉnh.",
                        MonthlyPrice = 3300000m,
                        MaxSwapsPerMonth = 20,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Tiết kiệm chi phí đáng kể trên mỗi lần đổi pin.\n✓ Gợi ý trạm đổi pin thông minh dựa trên lịch trình và tình trạng pin.\n✓ Chính sách hủy linh hoạt, không ràng buộc hợp đồng dài hạn.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Premium - Không giới hạn ({pinName})",
                        Description = "Đối tượng: Doanh nghiệp, xe dịch vụ (taxi, cho thuê) yêu cầu hoạt động liên tục.",
                        MonthlyPrice = 5500000m,
                        MaxSwapsPerMonth = null,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Hỗ trợ sự cố ưu tiên 24/7 (Cam kết xử lý nhanh nhất).\n✓ Cung cấp báo cáo sử dụng hàng tháng để quản lý đội xe hiệu quả.",
                        BatteryModelId = battery.Id
                    }
                });
            }
            else if (pinName.Contains("VF5"))
            {
                allPlans.AddRange(new[]
                {
                    new SubscriptionPlan
                    {
                        Name = $"Gói Basic - 10 lần/tháng ({pinName})",
                        Description = "Đối tượng: Người mới sử dụng xe điện, di chuyển chủ yếu trong nội thành.",
                        MonthlyPrice = 1200000m,
                        MaxSwapsPerMonth = 10,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Gói siêu tiết kiệm cho di chuyển đô thị.\n✓ Hỗ trợ tiêu chuẩn 24/7 đảm bảo an tâm trên đường.\n✓ Theo dõi số lần đổi còn lại dễ dàng qua ứng dụng.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Standard - 20 lần/tháng ({pinName})",
                        Description = "Đối tượng: Người có lối sống năng động, thường xuyên di chuyển giữa các quận hoặc đi ngoại ô.",
                        MonthlyPrice = 1600000m,
                        MaxSwapsPerMonth = 20,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Tiết kiệm hơn so với việc trả phí cho từng lần đổi lẻ.\n✓ Nhận thông báo về trạm mới và các mẹo sử dụng pin hiệu quả.\n✓ Thay đổi hoặc hủy gói linh hoạt vào cuối mỗi chu kỳ tháng.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Premium - Không giới hạn ({pinName})",
                        Description = "Đối tượng: Tài xế công nghệ, nhân viên kinh doanh, người có tần suất sử dụng xe rất cao.",
                        MonthlyPrice = 2700000m,
                        MaxSwapsPerMonth = null,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Cam kết pin hiệu suất cao (Luôn được đổi pin đã qua kiểm định chất lượng).\n✓ Hỗ trợ kỹ thuật nhanh qua App 24/7 (Yêu cầu được ưu tiên xử lý).",
                        BatteryModelId = battery.Id
                    }
                });
            }
            else if (pinName.Contains("VF3"))
            {
                allPlans.AddRange(new[]
                {
                    new SubscriptionPlan
                    {
                        Name = $"Gói Basic - 10 lần/tháng ({pinName})",
                        Description = "Đối tượng: Sinh viên, người cần phương tiện phụ, di chuyển quãng đường rất ngắn.",
                        MonthlyPrice = 900000m,
                        MaxSwapsPerMonth = 10,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Chi phí thấp nhất, khởi đầu cực kỳ dễ dàng.\n✓ Hỗ trợ tìm trạm gần nhất qua App 24/7.\n✓ Quản lý chi phí đơn giản, không phát sinh phụ phí.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Standard - 20 lần/tháng ({pinName})",
                        Description = "Đối tượng: Người đi làm, người giao hàng bán thời gian, di chuyển thường xuyên trong thành phố.",
                        MonthlyPrice = 1200000m,
                        MaxSwapsPerMonth = 20,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Tối ưu chi phí cho người di chuyển nhiều.\n✓ Ưu đãi đặc biệt khi gia hạn gói 6 tháng hoặc 1 năm.\n✓ Xem lại lịch sử và chi phí các lần đổi để theo dõi công việc.",
                        BatteryModelId = battery.Id
                    },
                    new SubscriptionPlan
                    {
                        Name = $"Gói Premium - Không giới hạn ({pinName})",
                        Description = "Đối tượng: Shipper chuyên nghiệp, người cần di chuyển liên tục không ngừng nghỉ.",
                        MonthlyPrice = 2000000m,
                        MaxSwapsPerMonth = null,
                        RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                        Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Hỗ trợ khẩn cấp 24/7 (Ưu tiên xử lý khi gặp sự cố về pin/trạm).\n✓ Tích điểm thưởng sau mỗi lần đổi để nhận các ưu đãi dịch vụ.",
                        BatteryModelId = battery.Id
                    }
                });
            }
        }
        if (allPlans.Count > 0)
        {
            db.SubscriptionPlans.AddRange(allPlans);
            db.SaveChanges();
            Console.WriteLine($"✅ Đã seed {allPlans.Count} gói subscription cho {batteryModels.Count} loại pin");
        }
        else
        {
            Console.WriteLine("⚠️ Không tìm thấy loại pin nào để seed SubscriptionPlans");
        }
    }
}

app.Run();
