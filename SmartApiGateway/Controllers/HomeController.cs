using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models;

namespace SmartApiGateway.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToAction("Dashboard");
            return View();
        }

        [AllowAnonymous]
        public IActionResult Documentation()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard(string filter = "24h", int limit = 10)
        {
            var query = _context.TrafficLogs.AsQueryable();

            if (filter != "all")
            {
                DateTime cutoff = DateTime.UtcNow.AddHours(-24);
                if (filter == "1h") cutoff = DateTime.UtcNow.AddHours(-1);
                else if (filter == "7d") cutoff = DateTime.UtcNow.AddDays(-7);
                else if (filter == "30d") cutoff = DateTime.UtcNow.AddDays(-30);
                else filter = "24h";

                query = query.Where(t => t.CreatedAt >= cutoff);
            }

            var totalRequests = await query.CountAsync();
            var avgTime = totalRequests > 0 ? await query.AverageAsync(t => t.ResponseTimeMs) : 0;
            var blockedCount = await _context.BlockedIps.CountAsync();

            int logsLimit = limit > 0 ? limit : 100;
            var recentLogs = await query.OrderByDescending(t => t.CreatedAt).Take(logsLimit).ToListAsync();

            var chartLogs = await query.OrderByDescending(t => t.CreatedAt).Take(50).ToListAsync();
            chartLogs = chartLogs.OrderBy(t => t.CreatedAt).ToList();
            var chartLabels = chartLogs.Select(t => t.CreatedAt.ToLocalTime().ToString("HH:mm")).ToList();
            var chartData = chartLogs.Select(t => t.ResponseTimeMs).ToList();

            var success = await query.CountAsync(t => t.StatusCode >= 200 && t.StatusCode < 300);
            var clientErr = await query.CountAsync(t => t.StatusCode >= 400 && t.StatusCode < 500);
            var serverErr = await query.CountAsync(t => t.StatusCode >= 500);

            var topIps = await query
                .GroupBy(t => t.IpAddress)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { Ip = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Ip, v => v.Count);

            var endpointQuery = query
                .GroupBy(t => t.RequestedUrl)
                .Select(g => new EndpointStat
                {
                    Path = g.Key,
                    SuccessCount = g.Count(t => t.StatusCode >= 200 && t.StatusCode < 300),
                    ClientErrorCount = g.Count(t => t.StatusCode >= 400 && t.StatusCode < 500),
                    ServerErrorCount = g.Count(t => t.StatusCode >= 500),
                    TotalCount = g.Count()
                })
                .OrderByDescending(e => e.TotalCount);

            var endpointStats = limit > 0
                ? await endpointQuery.Take(limit).ToListAsync()
                : await endpointQuery.ToListAsync();

            var model = new DashboardViewModel
            {
                TotalRequests = totalRequests,
                BlockedIpsCount = blockedCount,
                AverageResponseTime = System.Math.Round(avgTime, 2),
                RecentLogs = recentLogs,
                ActiveFilter = filter,
                EndpointLimit = limit,
                ChartLabels = chartLabels,
                ChartData = chartData,
                SuccessCount = success,
                ClientErrorCount = clientErr,
                ServerErrorCount = serverErr,
                TopIps = topIps,
                EndpointStats = endpointStats
            };

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}