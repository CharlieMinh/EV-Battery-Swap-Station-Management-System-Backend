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
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<SwapTransactionService>();
builder.Services.AddScoped<IEmailService, EmailService>(); // Email service for OTP
builder.Services.AddScoped<PasswordResetService>(); // Password reset service for Auth
builder.Services.AddScoped<GoogleAuthService>(); // Google OAuth service
builder.Services.AddScoped<StationService>(); // Station management with DisplayId generation
builder.Services.AddScoped<IBatteryInventoryService, BatteryInventoryService>(); // HYBRID SOLUTION: Quantity-based inventory management

// File Storage Service
builder.Services.AddHttpContextAccessor(); // Cần thiết để lấy base URL
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
        db.SaveChanges();
        Console.WriteLine("✅ Seeded 2 stations in Ho Chi Minh City");
    }

    // Update existing stations with default operating hours (8AM - 6PM)
    var stationsNeedUpdate = db.Stations
        .Where(s => s.OpenTime == TimeSpan.Zero && s.CloseTime == TimeSpan.Zero)
        .ToList();
    
    if (stationsNeedUpdate.Any())
    {
        foreach (var station in stationsNeedUpdate)
        {
            station.OpenTime = new TimeSpan(8, 0, 0);   // 8:00 AM
            station.CloseTime = new TimeSpan(18, 0, 0); // 6:00 PM
        }
        db.SaveChanges();
    }

    // Auto-generate DisplayId for existing stations that don't have one
    var stationService = scope.ServiceProvider.GetRequiredService<StationService>();
    await stationService.UpdateExistingStationsDisplayIdAsync();

    if (!db.Users.Any(u => u.Email == "admin@evbss.local"))
    {
        db.Users.Add(new User
        {
            Email = "admin@evbss.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345678Swp@"),
            Name = "EVBSS Admin",
            Role = Role.Admin
        });
    }

    if (!db.Users.Any(u => u.Email == "staff@evbss.local"))
    {
        db.Users.Add(new User
        {
            Email = "staff@evbss.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345678Swp@"),
            Name = "EVBSS Staff",
            Role = Role.Staff
        });
    }

    // Seed Battery Models (Only VF5 here, VF3/VF8/VF9 created by VehicleModelSeeder)
    if (!db.BatteryModels.Any())
    {
        db.BatteryModels.AddRange(
            // VF5 seed here to ensure it exists before VehicleModelSeeder runs
            new BatteryModel { Name = "VF5 Battery Pack", Voltage = 60, CapacityWh = 3000, Manufacturer = "VinFast" }
        );
        db.SaveChanges();
    }

    // Seed Battery Units moved after VehicleModelSeeder (see line ~370)

    // Seed VinFast-based Subscription Plans
    if (!db.SubscriptionPlans.Any())
    {
        // Chỉ dùng VF5 vì đã xóa BM-48V và BM-72V
        var vf5Battery = db.BatteryModels.First(x => x.Name == "VF5 Battery Pack");
        
        // ✅ NEW SIMPLIFIED SUBSCRIPTION PLANS
        db.SubscriptionPlans.AddRange(
            // BASIC PLAN - 10 swaps/month (VF5 battery)
            new SubscriptionPlan 
            { 
                Name = "Gói Basic - 10 lần/tháng", 
                Description = "Phù hợp cho người dùng thỉnh thoảng",
                MonthlyPrice = 450000m,              // 450k/tháng (tiết kiệm 10%)
                MaxSwapsPerMonth = 10,               // Tối đa 10 lần
                RequiresDeposit = false,
                DepositAmount = 0,
                RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                Benefits = " Tiết kiệm 10% so với trả lẻ, Hủy bất cứ lúc nào",
                BatteryModelId = vf5Battery.Id       // Đổi sang VF5
            },
            
            // STANDARD PLAN - 20 swaps/month (VF5 battery)
            new SubscriptionPlan 
            { 
                Name = "Gói Standard - 20 lần/tháng", 
                Description = "Phù hợp cho người dùng thường xuyên",
                MonthlyPrice = 850000m,              // 850k/tháng (tiết kiệm 15%)
                MaxSwapsPerMonth = 20,               // Tối đa 20 lần
                RequiresDeposit = false,
                DepositAmount = 0,
                RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                Benefits = " Tiết kiệm 15% so với trả lẻ Hủy bất cứ lúc nào",
                BatteryModelId = vf5Battery.Id       // Đổi sang VF5
            },
            
            // PREMIUM PLAN - Unlimited (VF5 Battery Pack)
            new SubscriptionPlan 
            { 
                Name = "Gói Premium - Không giới hạn", 
                Description = "Phù hợp cho doanh nghiệp, taxi, VinFast VF5",
                MonthlyPrice = 1500000m,             // 1.5tr/tháng (tiết kiệm 25%)
                MaxSwapsPerMonth = null,             // Không giới hạn!
                RequiresDeposit = false,
                DepositAmount = 0,
                RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                Benefits = "✓ KHÔNG GIỚI HẠN đổi pin Hỗ trợ 24/7",
                BatteryModelId = vf5Battery.Id       // VF5 (không đổi)
            },
            
            // VIP PLAN - Unlimited (VF5 battery)
            new SubscriptionPlan 
            { 
                Name = "Gói VIP - Không giới hạn SUV", 
                Description = "Phù hợp cho xe điện hạng sang ",
                MonthlyPrice = 2500000m,             // 2.5tr/tháng
                MaxSwapsPerMonth = null,             // Không giới hạn!
                RequiresDeposit = false,
                DepositAmount = 0,
                RefundPolicy = "Hoàn tiền theo tỷ lệ ngày còn lại",
                Benefits = " KHÔNG GIỚI HẠN đổi pin Hỗ trợ 24/7 VIP Ưu tiên tuyệt đối",
                BatteryModelId = vf5Battery.Id       // Đổi sang VF5
            }
        );
        db.SaveChanges();
    }
}




app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // đang chạy HTTP 8080 nên tắt tạm
app.UseCors("frontend");

// Cấu hình để phục vụ file tĩnh (ảnh, css, js...) từ thư mục wwwroot
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed VehicleModels
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await EVBSS.Api.Data.VehicleModelSeeder.SeedVehicleModelsAsync(context);
        logger.LogInformation("VehicleModels seeded successfully");
        
        // ========== SEED BATTERY UNITS (AFTER VehicleModelSeeder) ==========
        // Now VF3, VF5, VF8, VF9 all exist in database
        if (!context.BatteryUnits.Any())
        {
            var models = context.BatteryModels.ToList();
            
            // Get VinFast battery models
            var vf3 = models.FirstOrDefault(x => x.Name.Contains("VF3"));
            var vf5 = models.FirstOrDefault(x => x.Name.Contains("VF5"));
            var vf8 = models.FirstOrDefault(x => x.Name.Contains("VF8"));
            var vf9 = models.FirstOrDefault(x => x.Name.Contains("VF9"));
            
            var stations = context.Stations.ToList();
            
            if (stations.Count > 0 && vf3 != null && vf5 != null && vf8 != null && vf9 != null)
            {
                var st1 = stations[0];
                var st2 = stations.Count > 1 ? stations[1] : stations[0];

                context.BatteryUnits.AddRange(
                    // ========== STATION 1 ========== 
                    // VF3 Batteries (Compact city car - 30kWh)
                    new BatteryUnit { Serial = "VF3-S1-001", BatteryModelId = vf3.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF3-S1-002", BatteryModelId = vf3.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF3-S1-003", BatteryModelId = vf3.Id, StationId = st1.Id, Status = BatteryStatus.Charging },
                    
                    // VF5 Batteries (Small SUV - 3kWh) - Popular for testing
                    new BatteryUnit { Serial = "VF5-S1-001", BatteryModelId = vf5.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF5-S1-002", BatteryModelId = vf5.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF5-S1-003", BatteryModelId = vf5.Id, StationId = st1.Id, Status = BatteryStatus.Charging },
                    new BatteryUnit { Serial = "VF5-S1-004", BatteryModelId = vf5.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    
                    // VF8 Batteries (Mid-size SUV - 87.7kWh)
                    new BatteryUnit { Serial = "VF8-S1-001", BatteryModelId = vf8.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF8-S1-002", BatteryModelId = vf8.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF8-S1-003", BatteryModelId = vf8.Id, StationId = st1.Id, Status = BatteryStatus.Maintenance },
                    
                    // VF9 Batteries (Large SUV - 92kWh)
                    new BatteryUnit { Serial = "VF9-S1-001", BatteryModelId = vf9.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF9-S1-002", BatteryModelId = vf9.Id, StationId = st1.Id, Status = BatteryStatus.Full },

                    // ========== STATION 2 ========== 
                    // VF3 Batteries
                    new BatteryUnit { Serial = "VF3-S2-001", BatteryModelId = vf3.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF3-S2-002", BatteryModelId = vf3.Id, StationId = st2.Id, Status = BatteryStatus.Charging },
                    
                    // VF5 Batteries (more quantity for popular model)
                    new BatteryUnit { Serial = "VF5-S2-001", BatteryModelId = vf5.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF5-S2-002", BatteryModelId = vf5.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF5-S2-003", BatteryModelId = vf5.Id, StationId = st2.Id, Status = BatteryStatus.Charging },
                    new BatteryUnit { Serial = "VF5-S2-004", BatteryModelId = vf5.Id, StationId = st2.Id, Status = BatteryStatus.Issued },
                    
                    // VF8 Batteries
                    new BatteryUnit { Serial = "VF8-S2-001", BatteryModelId = vf8.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF8-S2-002", BatteryModelId = vf8.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                    
                    // VF9 Batteries
                    new BatteryUnit { Serial = "VF9-S2-001", BatteryModelId = vf9.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                    new BatteryUnit { Serial = "VF9-S2-002", BatteryModelId = vf9.Id, StationId = st2.Id, Status = BatteryStatus.Charging }
                );
                context.SaveChanges();
                
                logger.LogInformation("✅ Seeded {Count} VinFast battery units across {StationCount} stations", 
                    context.BatteryUnits.Count(), stations.Count);
            }
            else
            {
                logger.LogWarning("⚠️ Cannot seed BatteryUnits: Missing stations or battery models");
            }
        }
        
        // ========== SEED BATTERY INVENTORIES (AUTO-CALCULATE FROM BATTERY UNITS) ==========
        // NOTE: This runs OUTSIDE the BatteryUnits seed check, so it can populate inventory even if BatteryUnits already exist
        if (!context.BatteryInventories.Any())
        {
            logger.LogInformation("🔄 Calculating battery inventories from BatteryUnits...");
            
            // Group BatteryUnits by Station + BatteryModel + Status to calculate inventory
            var inventoryData = context.BatteryUnits
                .GroupBy(bu => new { bu.StationId, bu.BatteryModelId, bu.Status })
                .Select(g => new
                {
                    StationId = g.Key.StationId,
                    BatteryModelId = g.Key.BatteryModelId,
                    Status = g.Key.Status,
                    Quantity = g.Count()
                })
                .ToList();
            
            if (inventoryData.Any())
            {
                foreach (var inv in inventoryData)
                {
                    context.BatteryInventories.Add(new BatteryInventory
                    {
                        StationId = inv.StationId,
                        BatteryModelId = inv.BatteryModelId,
                        Status = inv.Status,
                        Quantity = inv.Quantity,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                
                context.SaveChanges();
                logger.LogInformation("✅ Seeded {Count} battery inventory records (grouped by Station + Model + Status)", 
                    context.BatteryInventories.Count());
            }
            else
            {
                logger.LogWarning("⚠️ No BatteryUnits found to generate inventory records");
            }
        }
        
        // Seed test vehicles for users
        if (!context.Vehicles.Any(v => v.Id == Guid.Parse("cbe25b14-fd54-4c47-be7d-ff710fe16e22")))
        {
            var driver1 = context.Users.FirstOrDefault(u => u.Email == "driver1@evbss.local");
            var vf5BatteryModel = context.BatteryModels.FirstOrDefault(b => b.Name == "VF5 Battery Pack");
            
            if (driver1 != null && vf5BatteryModel != null)
            {
                context.Vehicles.Add(new EVBSS.Api.Models.Vehicle
                {
                    Id = Guid.Parse("cbe25b14-fd54-4c47-be7d-ff710fe16e22"),
                    UserId = driver1.Id,
                    Plate = "51F-12345",
                    VIN = "VF5TEST123456789",
                    CompatibleBatteryModelId = vf5BatteryModel.Id,
                    PhotoUrl = "https://example.com/vf5.jpg",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                context.SaveChanges();
                logger.LogInformation("Test vehicle seeded for Driver1");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding VehicleModels and Vehicles");
    }
}

app.Run();
