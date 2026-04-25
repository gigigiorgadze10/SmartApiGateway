using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Models;

namespace SmartApiGateway.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<TrafficLog> TrafficLogs { get; set; }
        public DbSet<BlockedIp> BlockedIps { get; set; }
        public DbSet<ApiEndpoint> ApiEndpoints { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApiEndpoint>()
                .HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<BlockedIp>()
                .HasOne(b => b.BlockedBy)
                .WithMany()
                .HasForeignKey(b => b.BlockedById)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}