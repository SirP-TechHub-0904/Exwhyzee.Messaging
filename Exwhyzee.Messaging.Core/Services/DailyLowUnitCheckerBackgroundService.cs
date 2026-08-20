using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exwhyzee.Messaging.Core.Services
{
    /// <summary>
    /// Background Hosted Service that runs daily every morning at 7:00 AM UTC
    /// to check all client balances against their LowUnitReminderThreshold.
    /// </summary>
    public class DailyLowUnitCheckerBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyLowUnitCheckerBackgroundService> _logger;

        public DailyLowUnitCheckerBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DailyLowUnitCheckerBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Daily Low Unit Checker Background Service Started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    // Calculate delay until next 7:00 AM UTC
                    DateTime nextRun = now.Date.AddHours(7);
                    if (now >= nextRun)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    TimeSpan initialDelay = nextRun - now;
                    _logger.LogInformation($"Next morning low unit balance check scheduled in {initialDelay.TotalHours:N2} hours (at {nextRun:u}).");

                    await Task.Delay(initialDelay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Executing Daily Morning Low Unit Balance Check...");
                        using var scope = _serviceProvider.CreateScope();
                        var alertService = scope.ServiceProvider.GetRequiredService<ILowUnitAlertService>();
                        await alertService.RunDailyMorningLowUnitCheckAsync();
                        _logger.LogInformation("Daily Morning Low Unit Balance Check completed successfully.");
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during daily morning low unit check.");
                    // Wait 1 hour before retrying on failure
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }
    }
}
