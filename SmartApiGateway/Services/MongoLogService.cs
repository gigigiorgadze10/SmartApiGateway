using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SmartApiGateway.Models;

namespace SmartApiGateway.Services
{
    public class MongoLogService
    {
        private readonly IMongoCollection<TrafficLog> _logsCollection;

        public MongoLogService(IConfiguration configuration)
        {
            var connectionString = configuration["MongoDb:ConnectionString"];
            var databaseName = configuration["MongoDb:DatabaseName"];
            var collectionName = configuration["MongoDb:CollectionName"];

            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "mongodb://localhost:27017";
            }

            var mongoClient = new MongoClient(connectionString);
            var mongoDatabase = mongoClient.GetDatabase(databaseName);
            _logsCollection = mongoDatabase.GetCollection<TrafficLog>(collectionName);
        }

        public async Task InsertLogAsync(TrafficLog log)
        {
            await _logsCollection.InsertOneAsync(log);
        }

        public IQueryable<TrafficLog> GetLogsAsQueryable()
        {
            return _logsCollection.AsQueryable();
        }

        public async Task<long> DeleteOldLogsAsync(DateTime cutoffDate, CancellationToken ct = default)
        {
            var filter = Builders<TrafficLog>.Filter.Lt(x => x.CreatedAt, cutoffDate);
            var result = await _logsCollection.DeleteManyAsync(filter, ct);
            return result.DeletedCount;
        }
    }
}