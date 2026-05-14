using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Models;

namespace SmartApiGateway.Services
{
    public class SmartShieldService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmartShieldService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        public SmartShieldService(IServiceScopeFactory scopeFactory, ILogger<SmartShieldService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🛡️ Smart Shield Anomaly Detection Service გაეშვა...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await AnalyzeTrafficAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Smart Shield ანალიზის შეცდომა.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task AnalyzeTrafficAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var mongoService = scope.ServiceProvider.GetRequiredService<MongoLogService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

            var protectedEndpoints = await EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.ApiEndpoints.Where(e => e.EnableSmartShield).Select(e => e.Id), ct);

            if (!protectedEndpoints.Any()) return;

            var cutoff = DateTime.UtcNow.AddMinutes(-1);

            var query = mongoService.GetLogsAsQueryable()
                .Where(x => x.CreatedAt >= cutoff
                         && x.StatusCode >= 400 && x.StatusCode < 500
                         && x.EndpointId.HasValue
                         && protectedEndpoints.Contains(x.EndpointId.Value))
                .GroupBy(x => x.IpAddress)
                .Select(g => new { Ip = g.Key, ErrorCount = g.Count() });

            var stats = await MongoQueryable.ToListAsync(query, ct);

            var attackers = stats.Where(x => x.ErrorCount >= 30 && !string.IsNullOrEmpty(x.Ip)).ToList();

            if (!attackers.Any()) return;

            bool newlyBlocked = false;

            foreach (var attacker in attackers)
            {
                bool exists = await EntityFrameworkQueryableExtensions.AnyAsync(
                    dbContext.BlockedIps, b => b.IpAddress == attacker.Ip, ct);

                if (!exists)
                {
                    var blockedIp = new BlockedIp
                    {
                        IpAddress = attacker.Ip!,
                        Reason = $"Smart Shield Auto-Ban: საეჭვო სკანირება ({attacker.ErrorCount} ერორი 1 წუთში)"
                    };

                    dbContext.BlockedIps.Add(blockedIp);
                    newlyBlocked = true;

                    _logger.LogWarning("🛡️ Smart Shield-მა დაბლოკა IP: {Ip}", attacker.Ip);

                    await hubContext.Clients.Group("SuperAdmins").SendAsync("ReceiveShieldAlert",
                        new { ip = attacker.Ip, reason = blockedIp.Reason, count = attacker.ErrorCount }, ct);
                }
            }

            if (newlyBlocked)
            {
                await dbContext.SaveChangesAsync(ct);
            }
        }
    }
}