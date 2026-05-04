using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartApiGateway.Data;

namespace SmartApiGateway.Models
{
    public class BlockedIp
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [RegularExpression(@"^([0-9]{1,3}\.){3}[0-9]{1,3}$|^\:\:1$", ErrorMessage = "მიუთითეთ ვალიდური IP მისამართი")]
        public string IpAddress { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

        public string? BlockedById { get; set; }

        [ForeignKey("BlockedById")]
        public ApplicationUser? BlockedBy { get; set; }
    }
}