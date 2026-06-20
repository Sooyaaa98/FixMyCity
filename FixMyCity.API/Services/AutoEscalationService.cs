// FixMyCity.API/Services/AutoEscalationService.cs
// Background service: runs daily, escalates complaints that have been
// 'In Progress' for more than 30 days without resolution (US50).
// Calls usp_AutoEscalateAll — see SQL patch for the SP definition.

using FixMyCity.DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace FixMyCity.API.Services
{
    public class AutoEscalationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoEscalationService> _logger;
        private static readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public AutoEscalationService(IServiceScopeFactory scopeFactory,
                                     ILogger<AutoEscalationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoEscalationService started.");

            // Stagger startup to avoid racing application boot
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunEscalationAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AutoEscalationService: error during escalation run.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunEscalationAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FixMyCityDbContext>();

            await context.Database.ExecuteSqlRawAsync("EXEC dbo.usp_AutoEscalateAll", ct);

            _logger.LogInformation("AutoEscalationService: escalation job completed at {time}.",
                DateTime.UtcNow);
        }
    }
}