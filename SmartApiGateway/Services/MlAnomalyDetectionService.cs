using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Models;

namespace SmartApiGateway.Services
{
    public class TrafficData { public float RequestCount { get; set; } }
    public class SpikePrediction { [VectorType(3)] public double[] Prediction { get; set; } = new double[3]; }

    public class MlAnomalyDetectionService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MlAnomalyDetectionService> _logger;
        private readonly MLContext _mlContext;

        public MlAnomalyDetectionService(IServiceScopeFactory scopeFactory, ILogger<MlAnomalyDetectionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _mlContext = new MLContext(seed: 0);
        }

        public async Task ExecuteDetectionAsync()
        {
            var ct = CancellationToken.None;
            using var scope = _scopeFactory.CreateScope();
            var mongoService = scope.ServiceProvider.GetRequiredService<MongoLogService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

            var mlEndpoints = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.ApiEndpoints.Where(e => e.EnableMlAnomalyDetection).Select(e => e.Id), ct);

            if (!mlEndpoints.Any()) return;

            var logs = await MongoDB.Driver.Linq.MongoQueryable.ToListAsync(mongoService.GetLogsAsQueryable()
                .Where(x => x.CreatedAt >= DateTime.UtcNow.AddMinutes(-60) && x.EndpointId.HasValue && mlEndpoints.Contains(x.EndpointId.Value)), ct);

            var logsByIp = logs.GroupBy(x => x.IpAddress).Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var ipGroup in logsByIp)
            {
                var ip = ipGroup.Key;
                if (ipGroup.Count() < 60) continue;

                var timeSeriesData = new List<TrafficData>();
                for (int i = 59; i >= 0; i--)
                {
                    timeSeriesData.Add(new TrafficData { RequestCount = ipGroup.Count(x => x.CreatedAt >= DateTime.UtcNow.AddMinutes(-i - 1) && x.CreatedAt < DateTime.UtcNow.AddMinutes(-i)) });
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(timeSeriesData);
                var pipeline = _mlContext.Transforms.DetectSpikeBySsa(nameof(SpikePrediction.Prediction), nameof(TrafficData.RequestCount), 99.0, 15, 30, 10);
                var predictions = _mlContext.Data.CreateEnumerable<SpikePrediction>(pipeline.Fit(dataView).Transform(dataView), false).ToList();

                if (predictions.Last().Prediction[0] == 1)
                {
                    if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(dbContext.BlockedIps, b => b.IpAddress == ip, ct))
                    {
                        dbContext.BlockedIps.Add(new BlockedIp { IpAddress = ip, Reason = "ML Auto-Ban: არაბუნებრივი ტრაფიკი." });
                        await hubContext.Clients.Group("SuperAdmins").SendAsync("ReceiveShieldAlert", new { ip = ip });
                    }
                }
            }
            await dbContext.SaveChangesAsync(ct);
        }
    }
}