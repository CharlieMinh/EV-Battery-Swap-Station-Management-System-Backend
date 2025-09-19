using Microsoft.Extensions.Hosting;

namespace EVBSS.Api.Services;

public class ReservationExpireHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ReservationExpireHostedService> _logger;

    public ReservationExpireHostedService(IServiceProvider sp, ILogger<ReservationExpireHostedService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Dev: mỗi 60s quét 1 lần
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ReservationService>();
                var n = await svc.ExpireOverduesAsync();
                if (n > 0) _logger.LogInformation("Expired {Count} reservations", n);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expire job failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
