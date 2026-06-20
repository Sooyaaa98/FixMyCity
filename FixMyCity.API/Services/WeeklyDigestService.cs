// FixMyCity.API/Services/WeeklyDigestService.cs
// Background service: runs once per week, fires usp_GenerateWeeklyDigest (US65).
// Mirrors AutoEscalationService — same staggered start + try/catch + delay loop.

using FixMyCity.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace FixMyCity.API.Services
{
    public class WeeklyDigestService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WeeklyDigestService> _logger;
        private static readonly TimeSpan _interval = TimeSpan.FromDays(7);

        public WeeklyDigestService(IServiceScopeFactory scopeFactory,
                                   ILogger<WeeklyDigestService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WeeklyDigestService started.");

            // Stagger 10 min after startup so we don't race the other hosted services
            // and so digests don't fire the instant a redeploy happens.
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunDigestAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WeeklyDigestService: error during digest run.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunDigestAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FixMyCityDbContext>();

            await context.Database.ExecuteSqlRawAsync("EXEC dbo.usp_GenerateWeeklyDigest", ct);

            _logger.LogInformation("WeeklyDigestService: digest job completed at {time}.",
                DateTime.UtcNow);
        }
    }
}
