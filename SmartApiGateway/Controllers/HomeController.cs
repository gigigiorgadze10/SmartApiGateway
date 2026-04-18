using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ლენდინგი (საჯარო)
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // დეშბორდი (მხოლოდ შესული იუზერებისთვის)
        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var totalRequests = await _context.TrafficLogs.CountAsync();
            var blockedCount = await _context.BlockedIps.CountAsync();
            var avgTime = totalRequests > 0 ? await _context.TrafficLogs.AverageAsync(t => t.ResponseTimeMs) : 0;

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