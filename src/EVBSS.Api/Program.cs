var builder = WebApplication.CreateBuilder(args);

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

app.Run();
