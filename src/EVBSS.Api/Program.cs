using Microsoft.EntityFrameworkCore;
using EVBSS.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Swagger (Cách B bạn đã cài)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (để React gọi được)
builder.Services.AddCors(opt => {
  opt.AddPolicy("frontend", p => p
    .WithOrigins("http://localhost:3000", "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod());
});

// EF Core (đã làm 4.5)
var conn = builder.Configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(conn));

// 🔹 QUAN TRỌNG: bật Controllers
builder.Services.AddControllers();

var app = builder.Build();

// (Dev) auto-migrate + seed nếu bạn đã thêm
using (var scope = app.Services.CreateScope()) {
  var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
  db.Database.Migrate();
  if (!db.Stations.Any()) {
    db.Stations.AddRange(
      new EVBSS.Api.Models.Station { Name="BSS District 1", Address="123 Le Loi", City="HCM", Lat=10.776, Lng=106.700 },
      new EVBSS.Api.Models.Station { Name="BSS Thu Duc", Address="456 Vo Van Ngan", City="HCM", Lat=10.849, Lng=106.769 }
    );
    db.SaveChanges();
  }
}

app.UseSwagger();
app.UseSwaggerUI();

// app.UseHttpsRedirection(); // đang chạy HTTP 8080 thì tắt tạm
app.UseCors("frontend");

// 🔹 QUAN TRỌNG: map Controllers
app.MapControllers();

// ⛔ Tạm comment các Minimal API cũ để tránh trùng route
// app.MapGet("/stations", ...);
// app.MapGet("/ping", ...);

app.Run();
