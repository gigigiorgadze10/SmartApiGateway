using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SmartApiGateway.Models
{
    public class TrafficLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("ipAddress")]
        public string IpAddress { get; set; } = string.Empty;

        [BsonElement("requestedUrl")]
        public string RequestedUrl { get; set; } = string.Empty;

        [BsonElement("httpMethod")]
        public string HttpMethod { get; set; } = string.Empty;

        [BsonElement("statusCode")]
        public int StatusCode { get; set; }

        [BsonElement("responseTimeMs")]
        public long ResponseTimeMs { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("endpointId")]
        public int? EndpointId { get; set; }

        [BsonElement("userId")]
        public string? UserId { get; set; }
    }
}