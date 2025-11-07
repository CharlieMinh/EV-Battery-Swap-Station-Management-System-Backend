using EVBSS.Api.Data;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

/// <summary>
/// Background service tự động cancel payments pending quá 72 giờ
/// Chạy mỗi 1 giờ để kiểm tra và dọn dẹp payments timeout
/// </summary>
public class PaymentTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentTimeoutBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1); // Kiểm tra mỗi 1 giờ
    private static readonly TimeSpan PaymentTimeout = TimeSpan.FromHours(72); // Timeout sau 72 giờ

    public PaymentTimeoutBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PaymentTimeoutBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentTimeoutBackgroundService started. Will check for expired payments every {Interval} hours.", 
            CheckInterval.TotalHours);

        // Đợi 10 giây sau khi start để tránh conflict với các services khác khi khởi động
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CancelExpiredPaymentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cancelling expired payments");
            }

            // Đợi 1 giờ trước khi check lần tiếp theo
            await Task.Delay(CheckInterval, stoppingToken);
        }

        _logger.LogInformation("PaymentTimeoutBackgroundService stopped");
    }

    /// <summary>
    /// Tìm và cancel tất cả payments pending quá 72 giờ
    /// </summary>
    private async Task CancelExpiredPaymentsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var timeoutThreshold = now.AddHours(-72); // 72 giờ trước

        // Tìm tất cả payments pending quá 72 giờ
        var expiredPayments = await dbContext.Payments
            .Where(p => 
                p.Status == PaymentStatus.Pending && 
                p.CreatedAt < timeoutThreshold)
            .ToListAsync();

        if (!expiredPayments.Any())
        {
            _logger.LogDebug("No expired payments found at {Time}", now);
            return;
        }

        _logger.LogInformation("Found {Count} expired payments (pending > 72h). Cancelling...", 
            expiredPayments.Count);

        foreach (var payment in expiredPayments)
        {
            var hoursElapsed = (now - payment.CreatedAt).TotalHours;

            payment.Status = PaymentStatus.Cancelled;
            payment.CompletedAt = now;
            payment.Description = payment.Description + " [AUTO-CANCELLED: Timeout after 72h]";

            _logger.LogInformation(
                "Auto-cancelled payment {PaymentId} for user {UserId}. " +
                "Type: {Type}, Method: {Method}, Amount: {Amount} VND. " +
                "Created at {CreatedAt}, elapsed {Hours:F1} hours",
                payment.Id,
                payment.UserId,
                payment.Type,
                payment.Method,
                payment.Amount,
                payment.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                hoursElapsed
            );
        }

        await dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Successfully cancelled {Count} expired payments. Next check in {Interval} hour(s)", 
            expiredPayments.Count,
            CheckInterval.TotalHours
        );
    }
}
