using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.Models
{
    public class TrafficLog
    {
        [Key]
        public int Id { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty; // GET, POST და ა.შ.
        public int StatusCode { get; set; } // 200, 404, 500
        public long LatencyMs { get; set; } // დაყოვნება მილიწამებში
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}