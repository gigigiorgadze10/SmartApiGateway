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

            // თუ ლოკალურად connection string ცარიელია, თავიდან ავიცილოთ ერორი
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = "mongodb://localhost:27017"; // ლოკალური ტესტირებისთვის
            }

            var mongoClient = new MongoClient(connectionString);
            var mongoDatabase = mongoClient.GetDatabase(databaseName);
            _logsCollection = mongoDatabase.GetCollection<TrafficLog>(collectionName);
        }

        // 1. ლოგის ჩაწერა 
        public async Task InsertLogAsync(TrafficLog log)
        {
            await _logsCollection.InsertOneAsync(log);
        }

        // 2. ლოგების წამოღება (IQueryable)
        public IMongoQueryable<TrafficLog> GetLogsAsQueryable()
        {
            return _logsCollection.AsQueryable();
        }

        // 3. ძველი ლოგების წაშლა (Cleanup სერვისისთვის)
        public async Task<long> DeleteOldLogsAsync(DateTime cutoffDate, CancellationToken ct = default)
        {
            var filter = Builders<TrafficLog>.Filter.Lt(x => x.CreatedAt, cutoffDate);
            var result = await _logsCollection.DeleteManyAsync(filter, ct);
            return result.DeletedCount;
        }
    }
}