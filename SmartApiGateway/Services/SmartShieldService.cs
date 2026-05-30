using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Models;

namespace SmartApiGateway.Services
{
    public class SmartShieldService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmartShieldService> _logger;

        public SmartShieldService(IServiceScopeFactory scopeFactory, ILogger<SmartShieldService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ExecuteAnalysisAsync()
        {
            _logger.LogInformation("🛡️ Smart Shield ანალიზი დაიწყო...");
            var ct = CancellationToken.None;
            using var scope = _scopeFactory.CreateScope();
            var mongoService = scope.ServiceProvider.GetRequiredService<MongoLogService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

            var protectedEndpoints = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.ApiEndpoints.Where(e => e.EnableSmartShield).Select(e => e.Id), ct);

            if (!protectedEndpoints.Any()) return;

            var cutoff = DateTime.UtcNow.AddMinutes(-1);

            var query = mongoService.GetLogsAsQueryable()
                .Where(x => x.CreatedAt >= cutoff && x.StatusCode >= 400 && x.StatusCode < 500 && x.EndpointId.HasValue && protectedEndpoints.Contains(x.EndpointId.Value))
                .GroupBy(x => x.IpAddress)
                .Select(g => new { Ip = g.Key, ErrorCount = g.Count() });

            var stats = await MongoDB.Driver.Linq.MongoQueryable.ToListAsync(query, ct);
            var attackers = stats.Where(x => x.ErrorCount >= 30 && !string.IsNullOrEmpty(x.Ip)).ToList();

            foreach (var attacker in attackers)
            {
                if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(dbContext.BlockedIps, b => b.IpAddress == attacker.Ip, ct))
                {
                    dbContext.BlockedIps.Add(new BlockedIp { IpAddress = attacker.Ip!, Reason = $"Smart Shield Auto-Ban: {attacker.ErrorCount} ერორი 1 წუთში" });
                    _logger.LogWarning("🛡️ Smart Shield-მა დაბლოკა IP: {Ip}", attacker.Ip);
                    await hubContext.Clients.Group("SuperAdmins").SendAsync("ReceiveShieldAlert", new { ip = attacker.Ip });
                }
            }
            await dbContext.SaveChangesAsync(ct);
        }
    }
}