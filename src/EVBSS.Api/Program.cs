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

    // Seed Battery Models
    if (!db.BatteryModels.Any())
    {
        db.BatteryModels.AddRange(
            new BatteryModel { Name = "BM-48V-30Ah", Voltage = 48, CapacityWh = 1440, Manufacturer = "EVBSS" },
            new BatteryModel { Name = "BM-72V-40Ah", Voltage = 72, CapacityWh = 2880, Manufacturer = "EVBSS" },
            new BatteryModel { Name = "VF5 Battery Pack", Voltage = 60, CapacityWh = 3000, Manufacturer = "VinFast" }
        );
        db.SaveChanges();
    }

    // Seed Battery Units (mỗi station những pin theo ID cụ thể)
    if (!db.BatteryUnits.Any())
    {
        var models = db.BatteryModels.ToList();
        var m48 = models.First(x => x.Name == "BM-48V-30Ah");
        var m72 = models.First(x => x.Name == "BM-72V-40Ah");
        var vf5 = models.First(x => x.Name == "VF5 Battery Pack");
        var stations = db.Stations.ToList();
        
        if (stations.Count > 0)
        {
            var st1 = stations[0];
            var st2 = stations.Count > 1 ? stations[1] : stations[0];

            db.BatteryUnits.AddRange(
                // Station 1
                new BatteryUnit { Serial = "BM48-0001", BatteryModelId = m48.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "BM48-0002", BatteryModelId = m48.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "BM48-0003", BatteryModelId = m48.Id, StationId = st1.Id, Status = BatteryStatus.Charging },
                new BatteryUnit { Serial = "BM72-0001", BatteryModelId = m72.Id, StationId = st1.Id, Status = BatteryStatus.Maintenance },
                new BatteryUnit { Serial = "VF5-0001", BatteryModelId = vf5.Id, StationId = st1.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "VF5-0002", BatteryModelId = vf5.Id, StationId = st1.Id, Status = BatteryStatus.Full },

                // Station 2
                new BatteryUnit { Serial = "BM48-0101", BatteryModelId = m48.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "BM48-0102", BatteryModelId = m48.Id, StationId = st2.Id, Status = BatteryStatus.Charging },
                new BatteryUnit { Serial = "BM72-0101", BatteryModelId = m72.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "BM72-0102", BatteryModelId = m72.Id, StationId = st2.Id, Status = BatteryStatus.Issued },
                new BatteryUnit { Serial = "VF5-0101", BatteryModelId = vf5.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "VF5-0102", BatteryModelId = vf5.Id, StationId = st2.Id, Status = BatteryStatus.Charging }
            );
            db.SaveChanges();
        }
    }

    // Seed VinFast-based Subscription Plans
    if (!db.SubscriptionPlans.Any())
    {
        var bm48V = db.BatteryModels.First(x => x.Name == "BM-48V-30Ah");
        var bm72V = db.BatteryModels.First(x => x.Name == "BM-72V-40Ah");
        var vf5Battery = db.BatteryModels.First(x => x.Name == "VF5 Battery Pack");
        
        db.SubscriptionPlans.AddRange(
            // VF3 equivalent plans (48V battery)
            new SubscriptionPlan 
            { 
                Name = "FF3-Basic", 
                Description = "Gói cơ bản dành cho xe nhỏ - tương đương FF3",
                MonthlyFeeUnder1500Km = 1100000,
                MonthlyFee1500To3000Km = 1400000, 
                MonthlyFeeOver3000Km = 3000000,
                DepositAmount = 7000000,
                BatteryModelId = bm48V.Id
            },
            
            // VF5 plans (VF5 Battery Pack)
            new SubscriptionPlan 
            { 
                Name = "VF5-Standard", 
                Description = "Gói tiêu chuẩn dành cho VinFast VF5 - Pin chính hãng VF5",
                MonthlyFeeUnder1500Km = 1500000,
                MonthlyFee1500To3000Km = 2000000,
                MonthlyFeeOver3000Km = 3500000,
                DepositAmount = 18000000,
                BatteryModelId = vf5Battery.Id
            },
            
            // VF5 equivalent plans (48V battery) 
            new SubscriptionPlan 
            { 
                Name = "FF5-Standard", 
                Description = "Gói tiêu chuẩn dành cho xe compact - tương đương FF5",
                MonthlyFeeUnder1500Km = 1400000,
                MonthlyFee1500To3000Km = 1900000,
                MonthlyFeeOver3000Km = 3200000,
                DepositAmount = 15000000,
                BatteryModelId = bm48V.Id
            },
            
            // VF7 equivalent plans (72V battery)
            new SubscriptionPlan 
            { 
                Name = "FF7-Premium", 
                Description = "Gói cao cấp dành cho xe SUV - tương đương FF7",
                MonthlyFeeUnder1500Km = 2000000,
                MonthlyFee1500To3000Km = 3500000,
                MonthlyFeeOver3000Km = 5800000,
                DepositAmount = 41000000,
                BatteryModelId = bm72V.Id
            },
            
            // VF9 equivalent plans (72V battery)
            new SubscriptionPlan 
            { 
                Name = "FF9-Luxury", 
                Description = "Gói siêu cao cấp dành cho xe hạng sang - tương đương FF9",
                MonthlyFeeUnder1500Km = 3200000,
                MonthlyFee1500To3000Km = 5400000,
                MonthlyFeeOver3000Km = 8300000,
                DepositAmount = 60000000,
                BatteryModelId = bm72V.Id
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
