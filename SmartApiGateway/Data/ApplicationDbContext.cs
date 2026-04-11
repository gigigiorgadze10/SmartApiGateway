using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Models; // ეს ხაზი აუცილებელია

namespace SmartApiGateway.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ჩვენი ახალი ცხრილები
        public DbSet<TrafficLog> TrafficLogs { get; set; }
        public DbSet<BlockedIp> BlockedIps { get; set; }
        public DbSet<ApiEndpoint> ApiEndpoints { get; set; }
    }
}