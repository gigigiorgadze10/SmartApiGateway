using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;

namespace SmartApiGateway.Services
{
    /// <summary>
    /// Background Service — ყოველ 24 საათში ერთხელ 30 დღეზე ძველ Traffic Log-ებს შლის.
    /// ეს ასუფთავებს TrafficLogs ცხრილს და Dashboard-ს სიჩქარეს ინარჩუნებს.
    /// </summary>
    public class LogCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogCleanupService> _logger;

        // რამდენ დღეზე ძველი log-ები წაიშალოს
        private const int RetentionDays = 30;

        // რამდენ ხანში ერთხელ გაეშვას
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

            // პირველი გაშვება — app-ის სტარტიდან 1 წუთში (არ გვინდა მყისიერად გაეშვას)
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

                // EF Core ExecuteDeleteAsync — მასობრივი წაშლა ყოველი entity-ის load-ის გარეშე
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
                // სერვისი ჩამოიშალა — ეს ნორმალურია
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log Cleanup-ის დროს შეცდომა.");
            }
        }
    }
}