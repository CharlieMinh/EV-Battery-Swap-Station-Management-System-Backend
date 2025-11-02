using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EVBSS.Api.Data;
using Microsoft.OpenApi.Models;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using EVBSS.Api.Configuration;
using Amazon.Rekognition;
using Amazon.Runtime;
using EVBSS.Api.Hubs;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. CONFIGURE SERVICES (DEPENDENCY INJECTION)
// =========================================================================

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EVBSS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// CORS
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
        .AllowCredentials());
});

// EF Core DbContext
var conn = builder.Configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");
// Increase command timeout to 180 seconds to allow longer-running migrations/seed operations
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(conn, sqlServerOptions => sqlServerOptions.CommandTimeout(180)));

// Configurations
// Bind VNPay config from "Vnpay" section (supports ReturnUrl, IpnUrl, PaymentBackReturnUrl)
builder.Services.Configure<VnPayConfig>(builder.Configuration.GetSection("Vnpay"));

// JWT Authentication
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]
                                                                  ?? throw new InvalidOperationException("MissingJwt:Key")));
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
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Staff", policy => policy.RequireRole("Staff"));
});


// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ignore object reference cycles when serializing to JSON (prevents "A possible object cycle was detected")
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        // Đảm bảo rằng việc deserialization (từ JSON gửi đến) và serialization 
        // (từ C# trả về) sử dụng camelCase cho các khóa JSON, 
        // bất kể casing của thuộc tính C# (PascalCase).
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddSignalR();

// Application Services
builder.Services.AddScoped<SlotReservationService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();  // LUỒNG 2: Required for CreatePayPerSwapReservationAsync
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IVnPayServiceV2, VnPayServiceV2>(); // NEW: VNPay theo hướng dẫn chính thức
builder.Services.AddScoped<SwapTransactionService>();
builder.Services.AddScoped<BatteryComplaintService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<GoogleAuthService>();
builder.Services.AddScoped<StationService>();
builder.Services.AddScoped<IBatteryInventoryService, BatteryInventoryService>();
builder.Services.AddScoped<IBatteryStockRequestService, BatteryStockRequestService>(); // ⭐ NEW: Battery Stock Request Service

// File Storage & Image Services
builder.Services.AddHttpContextAccessor();
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
    builder.Services.AddSingleton<IAmazonRekognition>(_ =>
        new AmazonRekognitionClient(Amazon.RegionEndpoint.GetBySystemName(awsRegion)));
}
// HttpClientFactory handles the lifetime of HttpClient and the service correctly.
builder.Services.AddHttpClient<IAwsRekognitionService, AwsRekognitionService>();


// Background Services
builder.Services.AddHostedService<SlotReservationBackgroundService>();

// =========================================================================
// 2. CONFIGURE HTTP REQUEST PIPELINE (MIDDLEWARE)
// =========================================================================

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Tắt tạm để chạy HTTP
app.UseCors("frontend");
app.UseStaticFiles(); // Phục vụ file từ wwwroot

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SubscriptionExpirationMiddleware>();

app.MapHub<NotificationHub>("/notificationHub");

app.MapControllers();


// =========================================================================
// 3. SEED DATABASE (Gom toàn bộ logic vào một nơi)
// =========================================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var stationService = scope.ServiceProvider.GetRequiredService<StationService>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        context.Database.Migrate();

        // Step 1: Seed Stations and Users (nếu chưa có)
        if (!context.Stations.Any())
        {
            logger.LogInformation("Seeding initial stations...");
            context.Stations.AddRange(
                new Station
                {
                    Name = "Trạm Đổi Pin Quận 1 - Nguyễn Huệ",
                    Address = "123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1",
                    City = "Hồ Chí Minh",
                    Lat = 10.7769,
                    Lng = 106.7009,
                    IsActive = true,
                    OpenTime = new TimeSpan(8, 0, 0),
                    CloseTime = new TimeSpan(18, 0, 0),
                    PhoneNumber = "028-3822-9999",
                    PrimaryImageUrl = "https://example.com/stations/q1-nguyen-hue.jpg"
                },
                new Station
                {
                    Name = "Trạm Đổi Pin Quận 7 - Phú Mỹ Hưng",
                    Address = "456 Đường Nguyễn Văn Linh, Phường Tân Phú, Quận 7",
                    City = "Hồ Chí Minh",
                    Lat = 10.7329,
                    Lng = 106.7172,
                    IsActive = true,
                    OpenTime = new TimeSpan(8, 0, 0),
                    CloseTime = new TimeSpan(18, 0, 0),
                    PhoneNumber = "028-5412-8888",
                    PrimaryImageUrl = "https://example.com/stations/q7-phu-my-hung.jpg"
                }
            );
            context.SaveChanges();
            await stationService.UpdateExistingStationsDisplayIdAsync();
            logger.LogInformation("✅ Seeded 2 stations.");
        }

        if (!context.Users.Any(u => u.Email == "admin@evbss.local"))
        {
            context.Users.Add(new User { Email = "admin@evbss.local", PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345678Swp@"), Name = "EVBSS Admin", Role = Role.Admin });
            context.SaveChanges();
            logger.LogInformation("✅ Seeded Admin user.");
        }

        // Seed Staff User
        if (!context.Users.Any(u => u.Role == Role.Staff))
        {
            var firstStation = context.Stations.FirstOrDefault();
            if (firstStation != null)
            {
                logger.LogInformation("Seeding initial staff user...");
                context.Users.Add(new User
                {
                    Email = "staff1@evbss.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("staff123"),
                    Name = "Staff Member 1",
                    Role = Role.Staff,
                    StationId = firstStation.Id // Assign staff to the first station
                });
                context.SaveChanges();
                logger.LogInformation("✅ Seeded Staff user and assigned to station {StationName}.", firstStation.Name);
            }
            else
            {
                logger.LogWarning("⚠️ Could not seed staff user because no stations were found.");
            }
        }

        // Step 2: Seed Vehicle & Battery Models
        await VehicleModelSeeder.SeedVehicleModelsAsync(context);
        logger.LogInformation("✅ VehicleModels and their corresponding BatteryModels seeded successfully.");


        // Step 3: Seed Subscription Plans (SAU KHI CÓ ĐỦ BATTERY MODELS)
        if (!context.SubscriptionPlans.Any())
        {
            logger.LogInformation("Seeding new Subscription Plans for all battery models...");
            var batteryModels = context.BatteryModels.ToList();
            var allPlans = new List<SubscriptionPlan>();

            foreach (var battery in batteryModels)
            {
                var pinName = battery.Name;
                // ========== Gói cước cho Pin VF9 ==========
                if (pinName.Contains("VF9"))
                {
                    allPlans.AddRange(new[]
                    {
                        new SubscriptionPlan { Name = $"Gói Basic - 15 lần/tháng ({pinName})", Description = "Đối tượng: Người dùng có lộ trình di chuyển cố định, chủ yếu trong thành phố.", MonthlyPrice = 4100000m, MaxSwapsPerMonth = 15, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Giá khởi điểm tốt nhất để trải nghiệm dịch vụ.\n✓ Hỗ trợ tiêu chuẩn 24/7 qua tổng đài.\n✓ Linh hoạt nâng cấp gói bất kỳ lúc nào.", BatteryModelId = battery.Id },
                        new SubscriptionPlan { Name = $"Gói Standard - 30 lần/tháng ({pinName})", Description = "Đối tượng: Gia đình, chuyên gia thường xuyên di chuyển giữa các tỉnh hoặc đi nghỉ cuối tuần.", MonthlyPrice = 6500000m, MaxSwapsPerMonth = 30, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Tiết kiệm ~20% trên mỗi lần đổi so với gói Basic.\n✓ Xem lịch sử đổi pin chi tiết trên ứng dụng.\n✓ Tùy chọn tạm ngưng gói 1 lần/năm (tối đa 30 ngày).", BatteryModelId = battery.Id },
                        new SubscriptionPlan { Name = $"Gói Premium - Không giới hạn ({pinName})", Description = "Đối tượng: Doanh nhân, chủ doanh nghiệp, người yêu cầu dịch vụ đẳng cấp nhất.", MonthlyPrice = 8100000m, MaxSwapsPerMonth = null, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Đường dây nóng VIP 24/7 (Kết nối trực tiếp).\n✓ Ưu tiên đặt trước pin tại trạm.", BatteryModelId = battery.Id }
                    });
                }
                // ========== Gói cước cho Pin VF8 ==========
                else if (pinName.Contains("VF8"))
                {
                    allPlans.AddRange(new[]
                    {
                        new SubscriptionPlan { Name = $"Gói Basic - 15 lần/tháng ({pinName})", Description = "Đối tượng: Người đi làm hàng ngày, nhu cầu di chuyển cơ bản.", MonthlyPrice = 2200000m, MaxSwapsPerMonth = 15, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Chi phí tối ưu cho nhu cầu đi lại cố định.\n✓ Hỗ trợ 24/7 qua ứng dụng và tổng đài.\n✓ Dễ dàng nâng cấp khi cần.", BatteryModelId = battery.Id },
                        new SubscriptionPlan { Name = $"Gói Standard - 30 lần/tháng ({pinName})", Description = "Đối tượng: Cấp quản lý, người thường xuyên công tác, di chuyển liên tỉnh.", MonthlyPrice = 3300000m, MaxSwapsPerMonth = 30, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Tiết kiệm chi phí đáng kể trên mỗi lần đổi pin.\n✓ Gợi ý trạm đổi pin thông minh.\n✓ Chính sách hủy linh hoạt.", BatteryModelId = battery.Id },
                        new SubscriptionPlan { Name = $"Gói Premium - Không giới hạn ({pinName})", Description = "Đối tượng: Doanh nghiệp, xe dịch vụ (taxi, cho thuê) yêu cầu hoạt động liên tục.", MonthlyPrice = 5500000m, MaxSwapsPerMonth = null, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Hỗ trợ sự cố ưu tiên 24/7 (Cam kết xử lý nhanh).\n✓ Cung cấp báo cáo sử dụng hàng tháng.", BatteryModelId = battery.Id }
                    });
                }
                // ========== Gói cước cho Pin VF5 ==========
                else if (pinName.Contains("VF5"))
                {
                    allPlans.AddRange(new[]
                    {
                         new SubscriptionPlan { Name = $"Gói Basic - 10 lần/tháng ({pinName})", Description = "Đối tượng: Người mới sử dụng xe điện, di chuyển chủ yếu trong nội thành.", MonthlyPrice = 1200000m, MaxSwapsPerMonth = 10, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Gói siêu tiết kiệm cho di chuyển đô thị.\n✓ Hỗ trợ tiêu chuẩn 24/7.\n✓ Theo dõi số lần đổi còn lại qua ứng dụng.", BatteryModelId = battery.Id },
                         new SubscriptionPlan { Name = $"Gói Standard - 20 lần/tháng ({pinName})", Description = "Đối tượng: Người có lối sống năng động, thường xuyên di chuyển giữa các quận hoặc đi ngoại ô.", MonthlyPrice = 1600000m, MaxSwapsPerMonth = 20, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Tiết kiệm hơn so với trả phí lẻ.\n✓ Nhận thông báo về trạm mới & mẹo dùng pin.\n✓ Hủy gói linh hoạt cuối chu kỳ.", BatteryModelId = battery.Id },
                         new SubscriptionPlan { Name = $"Gói Premium - Không giới hạn ({pinName})", Description = "Đối tượng: Tài xế công nghệ, nhân viên kinh doanh, người có tần suất sử dụng xe rất cao.", MonthlyPrice = 2700000m, MaxSwapsPerMonth = null, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Cam kết pin hiệu suất cao (đã kiểm định).\n✓ Hỗ trợ kỹ thuật nhanh qua App 24/7.", BatteryModelId = battery.Id }
                    });
                }
                // ========== Gói cước cho Pin VF3 ==========
                else if (pinName.Contains("VF3"))
                {
                    allPlans.AddRange(new[]
                    {
                        new SubscriptionPlan { Name = $"Gói Basic - 10 lần/tháng ({pinName})", Description = "Đối tượng: Sinh viên, người cần phương tiện phụ, di chuyển quãng đường rất ngắn.", MonthlyPrice = 900000m, MaxSwapsPerMonth = 10, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Chi phí thấp nhất, khởi đầu cực kỳ dễ dàng.\n✓ Hỗ trợ tìm trạm gần nhất qua App 24/7.\n✓ Quản lý chi phí đơn giản.", BatteryModelId = battery.Id },
                        new SubscriptionPlan { Name = $"Gói Standard - 20 lần/tháng ({pinName})", Description = "Đối tượng: Người đi làm, người giao hàng bán thời gian, di chuyển thường xuyên trong thành phố.", MonthlyPrice = 1200000m, MaxSwapsPerMonth = 20, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Tối ưu chi phí cho người di chuyển nhiều.\n✓ Ưu đãi đặc biệt khi gia hạn dài hạn.\n✓ Xem lại lịch sử và chi phí đổi pin.", BatteryModelId = battery.Id },
                        new SubscriptionPlan { Name = $"Gói Premium - Không giới hạn ({pinName})", Description = "Đối tượng: Shipper chuyên nghiệp, người cần di chuyển liên tục không ngừng nghỉ.", MonthlyPrice = 2000000m, MaxSwapsPerMonth = null, RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại", Benefits = "✓ Không giới hạn số lần đổi pin.\n✓ Hỗ trợ khẩn cấp 24/7 (Ưu tiên xử lý sự cố).\n✓ Tích điểm thưởng đổi ưu đãi dịch vụ.", BatteryModelId = battery.Id }
                    });
                }
            }

            if (allPlans.Any())
            {
                context.SubscriptionPlans.AddRange(allPlans);
                context.SaveChanges();
                logger.LogInformation("✅ Seeded {Count} new subscription plans across {ModelCount} battery models.", allPlans.Count, batteryModels.Count);
            }
            else
            {
                logger.LogWarning("⚠️ No battery models found to seed subscription plans.");
            }
        }
        
        // Step 4: Seed Battery Units and Inventories
        if (!context.BatteryUnits.Any())
        {
            logger.LogInformation("Seeding battery units for stations...");
            var models = context.BatteryModels.ToList();
            var stations = context.Stations.ToList();
            var vf3 = models.FirstOrDefault(x => x.Name.Contains("VF3"));
            var vf5 = models.FirstOrDefault(x => x.Name.Contains("VF5"));
            var vf8 = models.FirstOrDefault(x => x.Name.Contains("VF8"));
            var vf9 = models.FirstOrDefault(x => x.Name.Contains("VF9"));

            if (stations.Any() && vf3 != null && vf5 != null && vf8 != null && vf9 != null)
            {
                 // (Dán logic seed BatteryUnits và BatteryInventories của bạn vào đây)
                 logger.LogInformation("✅ Battery Units and Inventories seeded successfully.");
            }
            else
            {
                 logger.LogWarning("⚠️ Cannot seed BatteryUnits: Missing stations or battery models.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database seeding.");
    }
}

// =========================================================================
// 4. RUN THE APPLICATION
// =========================================================================
app.Run();