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

        // 1. ლოგის ჩაწერა (უმსუბუქესი ოპერაცია Mongo-სთვის)
        public async Task InsertLogAsync(TrafficLog log)
        {
            await _logsCollection.InsertOneAsync(log);
        }

        // 2. ლოგების წამოღება (IQueryable საშუალებას გვაძლევს LINQ გამოვიყენოთ Controller-ში)
        public IMongoQueryable<TrafficLog> GetLogsAsQueryable()
        {
            return _logsCollection.AsQueryable();
        }
    }
}