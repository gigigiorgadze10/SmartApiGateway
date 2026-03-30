using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.Models
{
    public class BlockedIp
    {
        [Key]
        public int Id { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}