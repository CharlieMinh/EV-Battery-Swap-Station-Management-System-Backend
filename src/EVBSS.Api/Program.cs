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
        .WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:5194", "https://localhost:7240")
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
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddHostedService<EVBSS.Api.Services.ReservationExpireHostedService>();


var app = builder.Build();

// (Dev) auto-migrate nếu cần
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Email == "admin@evbss.local"))
    {
        db.Users.Add(new User
        {
            Email = "admin@evbss.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345678"),
            Name = "EVBSS Admin",
            Role = Role.Admin
        });
    }

    if (!db.Users.Any(u => u.Email == "staff@evbss.local"))
    {
        db.Users.Add(new User
        {
            Email = "staff@evbss.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345678"),
            Name = "EVBSS Staff",
            Role = Role.Staff
        });
    }

    // Seed Battery Models
    if (!db.BatteryModels.Any())
    {
        db.BatteryModels.AddRange(
            new BatteryModel { Name = "BM-48V-30Ah", Voltage = 48, CapacityWh = 1440, Manufacturer = "EVBSS" },
            new BatteryModel { Name = "BM-72V-40Ah", Voltage = 72, CapacityWh = 2880, Manufacturer = "EVBSS" }
        );
        db.SaveChanges();
    }

    // Seed Battery Units (mỗi station vài viên, các trạng thái khác nhau)
    if (!db.BatteryUnits.Any())
    {
        var models = db.BatteryModels.ToList();
        var m48 = models.First(x => x.Name == "BM-48V-30Ah");
        var m72 = models.First(x => x.Name == "BM-72V-40Ah");
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

                // Station 2
                new BatteryUnit { Serial = "BM48-0101", BatteryModelId = m48.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "BM48-0102", BatteryModelId = m48.Id, StationId = st2.Id, Status = BatteryStatus.Charging },
                new BatteryUnit { Serial = "BM72-0101", BatteryModelId = m72.Id, StationId = st2.Id, Status = BatteryStatus.Full },
                new BatteryUnit { Serial = "BM72-0102", BatteryModelId = m72.Id, StationId = st2.Id, Status = BatteryStatus.Issued }
            );
            db.SaveChanges();
        }
    }

    // Seed VinFast-based Subscription Plans
    if (!db.SubscriptionPlans.Any())
    {
        var bm48V = db.BatteryModels.First(x => x.Name == "BM-48V-30Ah");
        var bm72V = db.BatteryModels.First(x => x.Name == "BM-72V-40Ah");
        
        db.SubscriptionPlans.AddRange(
            // VF3 equivalent plans (48V battery)
            new SubscriptionPlan 
            { 
                Name = "VF3-Basic", 
                Description = "Gói cơ bản dành cho xe nhỏ - tương đương VF3",
                MonthlyFeeUnder1500Km = 1100000,
                MonthlyFee1500To3000Km = 1400000, 
                MonthlyFeeOver3000Km = 3000000,
                DepositAmount = 7000000,
                BatteryModelId = bm48V.Id
            },
            
            // VF5 equivalent plans (48V battery) 
            new SubscriptionPlan 
            { 
                Name = "VF5-Standard", 
                Description = "Gói tiêu chuẩn dành cho xe compact - tương đương VF5",
                MonthlyFeeUnder1500Km = 1400000,
                MonthlyFee1500To3000Km = 1900000,
                MonthlyFeeOver3000Km = 3200000,
                DepositAmount = 15000000,
                BatteryModelId = bm48V.Id
            },
            
            // VF7 equivalent plans (72V battery)
            new SubscriptionPlan 
            { 
                Name = "VF7-Premium", 
                Description = "Gói cao cấp dành cho xe SUV - tương đương VF7",
                MonthlyFeeUnder1500Km = 2000000,
                MonthlyFee1500To3000Km = 3500000,
                MonthlyFeeOver3000Km = 5800000,
                DepositAmount = 41000000,
                BatteryModelId = bm72V.Id
            },
            
            // VF9 equivalent plans (72V battery)
            new SubscriptionPlan 
            { 
                Name = "VF9-Luxury", 
                Description = "Gói siêu cao cấp dành cho xe hạng sang - tương đương VF9",
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
