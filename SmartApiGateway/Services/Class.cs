using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;

namespace SmartApiGateway.Services
{
    public class LogCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogCleanupService> _logger;

        private const int RetentionDays = 30;

        private readonly TimeSpan _interval = TimeSpan.FromHours(24);

        public LogCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<LogCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Log Cleanup Service გაეშვა. Retention: {Days} დღე.", RetentionDays);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCleanupAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunCleanupAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);

                int deleted = await db.TrafficLogs
                    .Where(t => t.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(ct);

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Log Cleanup: {Count} ძველი ჩანაწერი წაიშალა ({Cutoff} UTC-მდე).",
                        deleted, cutoff.ToString("yyyy-MM-dd"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log Cleanup-ის დროს შეცდომა.");
            }
        }
    }
}