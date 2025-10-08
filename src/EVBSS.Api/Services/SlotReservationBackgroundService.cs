using Microsoft.Extensions.Hosting;

namespace EVBSS.Api.Services;

/// <summary>
/// Background service tự động expire reservations quá hạn và gửi reminders
/// </summary>
public class SlotReservationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SlotReservationBackgroundService> _logger;

    public SlotReservationBackgroundService(
        IServiceProvider sp, 
        ILogger<SlotReservationBackgroundService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SlotReservationBackgroundService started");
        
        // Chạy mỗi 5 phút
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var slotService = scope.ServiceProvider.GetRequiredService<SlotReservationService>();
                
                // Task 1: Auto-expire overdue reservations
                var expiredCount = await slotService.ExpireOverdueReservationsAsync();
                if (expiredCount > 0)
                {
                    _logger.LogInformation("Auto-expired {Count} overdue reservations", expiredCount);
                }
                
                // TODO: Task 2: Send reminders 30min before slot
                // await SendUpcomingRemindersAsync(scope);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SlotReservationBackgroundService error");
            }

            // Wait 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
        
        _logger.LogInformation("SlotReservationBackgroundService stopped");
    }
    
    // TODO: Implement reminder logic
    // private async Task SendUpcomingRemindersAsync(IServiceScope scope)
    // {
    //     // Get reservations with slot starting in 30 minutes
    //     // Send push notification / email
    // }
}
