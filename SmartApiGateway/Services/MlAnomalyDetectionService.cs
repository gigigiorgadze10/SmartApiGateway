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
    public class TrafficData
    {
        public float RequestCount { get; set; }
    }

    public class SpikePrediction
    {
        [VectorType(3)]
        public double[] Prediction { get; set; } = new double[3];
    }

    public class MlAnomalyDetectionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MlAnomalyDetectionService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private readonly MLContext _mlContext;

        public MlAnomalyDetectionService(IServiceScopeFactory scopeFactory, ILogger<MlAnomalyDetectionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _mlContext = new MLContext(seed: 0); 
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🧠 ML Anomaly Detection Service გაეშვა...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DetectAnomaliesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ML Anomaly Detection შეცდომა.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task DetectAnomaliesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var mongoService = scope.ServiceProvider.GetRequiredService<MongoLogService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

            var mlEndpoints = await EntityFrameworkQueryableExtensions.ToListAsync(
                dbContext.ApiEndpoints.Where(e => e.EnableMlAnomalyDetection).Select(e => e.Id), ct);

            if (!mlEndpoints.Any()) return;

            var cutoff = DateTime.UtcNow.AddMinutes(-60);

            var logs = await MongoQueryable.ToListAsync(mongoService.GetLogsAsQueryable()
                .Where(x => x.CreatedAt >= cutoff && x.EndpointId.HasValue && mlEndpoints.Contains(x.EndpointId.Value)), ct);

            var logsByIp = logs.GroupBy(x => x.IpAddress).Where(g => !string.IsNullOrEmpty(g.Key)).ToList();

            bool newlyBlocked = false;

            foreach (var ipGroup in logsByIp)
            {
                var ip = ipGroup.Key;

                if (ipGroup.Count() < 60) continue;

                var timeSeriesData = new List<TrafficData>();
                for (int i = 59; i >= 0; i--)
                {
                    var minuteStart = DateTime.UtcNow.AddMinutes(-i - 1);
                    var minuteEnd = DateTime.UtcNow.AddMinutes(-i);
                    var countInMinute = ipGroup.Count(x => x.CreatedAt >= minuteStart && x.CreatedAt < minuteEnd);
                    timeSeriesData.Add(new TrafficData { RequestCount = countInMinute });
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(timeSeriesData);

                var pipeline = _mlContext.Transforms.DetectSpikeBySsa(
                    outputColumnName: nameof(SpikePrediction.Prediction),
                    inputColumnName: nameof(TrafficData.RequestCount),
                    confidence: 99.0,         
                    pvalueHistoryLength: 15,   
                    trainingWindowSize: 30,      
                    seasonalityWindowSize: 10);  

                var model = pipeline.Fit(dataView);
                var transformedData = model.Transform(dataView);
                var predictions = _mlContext.Data.CreateEnumerable<SpikePrediction>(transformedData, reuseRowObject: false).ToList();

                var latestPrediction = predictions.Last();
                bool isSpike = latestPrediction.Prediction[0] == 1;

                if (isSpike)
                {
                    bool exists = await EntityFrameworkQueryableExtensions.AnyAsync(
                        dbContext.BlockedIps, b => b.IpAddress == ip, ct);

                    if (!exists)
                    {
                        int requestsLastMinute = (int)timeSeriesData.Last().RequestCount;
                        var blockedIp = new BlockedIp
                        {
                            IpAddress = ip,
                            Reason = $"ML Auto-Ban: არაბუნებრივი ტრაფიკის ანომალია ({requestsLastMinute} req/min) 99% სიზუსტით."
                        };

                        dbContext.BlockedIps.Add(blockedIp);
                        newlyBlocked = true;

                        _logger.LogWarning("🤖 ML.NET-მა დაიჭირა DDoS ანომალია და დაბლოკა IP: {Ip}", ip);

                        await hubContext.Clients.Group("SuperAdmins").SendAsync("ReceiveShieldAlert",
                            new { ip = ip, reason = blockedIp.Reason, count = requestsLastMinute }, ct);
                    }
                }
            }

            if (newlyBlocked)
            {
                await dbContext.SaveChangesAsync(ct);
            }
        }
    }
}