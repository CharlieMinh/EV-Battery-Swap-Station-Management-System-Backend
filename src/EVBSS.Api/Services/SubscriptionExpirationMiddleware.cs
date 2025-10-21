namespace EVBSS.Api.Services;

/// <summary>
/// Middleware that automatically checks and expires subscriptions on each request.
/// This eliminates the need for a background job while ensuring subscriptions
/// are expired promptly when users interact with the system.
/// </summary>
public class SubscriptionExpirationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionExpirationMiddleware> _logger;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public SubscriptionExpirationMiddleware(
        RequestDelegate next, 
        ILogger<SubscriptionExpirationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISubscriptionService subscriptionService)
    {
        // Only check every 5 minutes to avoid excessive DB queries
        var now = DateTime.UtcNow;
        if (now - _lastCheck > CheckInterval)
        {
            try
            {
                await subscriptionService.CheckAndExpireSubscriptionsAsync();
                _lastCheck = now;
            }
            catch (Exception ex)
            {
                // Log error but don't block the request
                _logger.LogError(ex, "Error checking subscription expiration");
            }
        }

        await _next(context);
    }
}
