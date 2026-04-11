using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    [Authorize] // მხოლოდ ავტორიზებული პირებისთვის
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var totalRequests = await _context.TrafficLogs.CountAsync();
            var blockedCount = await _context.BlockedIps.CountAsync();
            var avgTime = totalRequests > 0 ? await _context.TrafficLogs.AverageAsync(t => t.ResponseTimeMs) : 0;

            // ბოლო 10 ტრანზაქციის წამოღება
            var recentLogs = await _context.TrafficLogs
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                TotalRequests = totalRequests,
                BlockedIpsCount = blockedCount,
                AverageResponseTime = System.Math.Round(avgTime, 2),
                RecentLogs = recentLogs
            };

            return View(model);
        }
    }
}