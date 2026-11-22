using JwtDemo.Core;
using JwtDemo.Hubs;
using JwtDemo.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NodaTime;

namespace JwtDemo.BackgroundServices;

public sealed class TimeUpdateService(
    IClock clock,
    IHubContext<TimeHub, ITimeClient> hubContext,
    IOptions<Settings> options,
    ILogger<TimeUpdateService> logger) : BackgroundService
{
    private const int LowQualityUpdateInterval = 5;
    private readonly DateTimeZone _timezone = DateTimeZoneProviders.Tzdb[options.Value.Timezone];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(GetDurationUntilNextFullSecond().ToTimeSpan(), stoppingToken);

            var now = GetLocalTime();
            var sendToUsersOnly = now.Second % LowQualityUpdateInterval != 0;

            try
            {
                await SendTimeUpdate(now, sendToUsersOnly);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // an unhandled exception would stop the background service (and by default the whole application)
                logger.LogError(ex, "Failed to send time update");
            }
        }
    }

    private async Task SendTimeUpdate(LocalDateTime currentTime, bool sendToUsersOnly)
    {
        // If sending the update to all clients would take longer than our 1-second interval, we'd have to be smarter here.
        // Luckily, we only have a few clients in this demo, so we can afford to be lazy.

        await hubContext.Clients.Group(TimeHub.AuthenticatedGroupName)
                        .ReceiveTime(new TimeUpdate { CurrentTime = currentTime, Quality = TimeQuality.High });

        if (sendToUsersOnly)
        {
            return;
        }

        await hubContext.Clients.Group(TimeHub.UnauthenticatedGroupName)
                        .ReceiveTime(new TimeUpdate { CurrentTime = currentTime, Quality = TimeQuality.Low });
    }

    private Duration GetDurationUntilNextFullSecond()
    {
        var now = clock.GetCurrentInstant();
        var nextFullSecond = Instant.FromUnixTimeSeconds(now.ToUnixTimeSeconds() + 1);

        return nextFullSecond - now;
    }

    private LocalDateTime GetLocalTime() => clock.GetCurrentInstant().InZone(_timezone).LocalDateTime;
}
