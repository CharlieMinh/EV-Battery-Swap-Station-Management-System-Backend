using Microsoft.EntityFrameworkCore;
using EVBSS.Api.Data; // nơi bạn đã tạo AppDbContext ở bước 4.3
using Microsoft.EntityFrameworkCore;
using EVBSS.Api.Data;
using EVBSS.Api.Models;



var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(conn));




// 🔽 CORS
builder.Services.AddCors(opt =>
{
   opt.AddPolicy("frontend", p => p
       .WithOrigins("http://localhost:3000", "http://localhost:5173")
       .AllowAnyHeader()
       .AllowAnyMethod());
});

builder.Services.AddOpenApi();

// Thêm hai dòng này cho Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate & seed dev data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // đảm bảo schema luôn khớp

    if (!db.Stations.Any())
    {
        db.Stations.AddRange(
            new Station { Name = "BSS District 1", Address = "123 Le Loi", City = "HCM", Lat = 10.776, Lng = 106.700, IsActive = true },
            new Station { Name = "BSS Thu Duc",   Address = "456 Vo Van Ngan", City = "HCM", Lat = 10.849, Lng = 106.769, IsActive = true }
        );
        db.SaveChanges();
    }
}


app.UseCors("frontend");

// Bật Swagger ở mọi env (hoặc bọc trong if Dev tùy bạn)
app.UseSwagger();
app.UseSwaggerUI();  // UI tại /swagger

// app.UseHttpsRedirection(); // tắt tạm khi chạy HTTP:8080

app.MapGet("/weatherforecast", () => { /* như bạn đang có */ })
   .WithName("GetWeatherForecast")
   .WithOpenApi(); // để có trong doc

app.MapGet("/ping", () => Results.Ok(new { message = "pong", time = DateTime.UtcNow }))
   .WithOpenApi();

app.MapGet("/_db-check", (AppDbContext db) => Results.Ok(new { dbRegistered = db is not null }));

app.MapGet("/stations", async (AppDbContext db) =>
    await db.Stations.AsNoTracking().ToListAsync()
).WithOpenApi();


app.Run();
