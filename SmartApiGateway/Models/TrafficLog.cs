using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.Models
{
    public class TrafficLog
    {
        [Key]
        public int Id { get; set; }

        public string IpAddress { get; set; } = string.Empty;

        public string RequestedUrl { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        public long ResponseTimeMs { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? EndpointId { get; set; }
        public string? UserId { get; set; }
    }
}