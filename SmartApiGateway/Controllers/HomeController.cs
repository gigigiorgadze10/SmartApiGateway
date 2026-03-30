using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;

namespace SmartApiGateway.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;

            // 1. ჯამური მოთხოვნები დღეს
            var totalRequestsToday = await _context.TrafficLogs
                .Where(t => t.Timestamp >= today)
                .CountAsync();

            // 2. დაბლოკილი IP-ების რაოდენობა
            var totalBlockedIps = await _context.BlockedIps.CountAsync();

            // 3. საშუალო დაყოვნება (Latency)
            var avgLatency = await _context.TrafficLogs
                .Where(t => t.Timestamp >= today)
                .AverageAsync(t => (double?)t.LatencyMs) ?? 0;

            // 4. ბოლო 10 ცოცხალი ლოგი (ცხრილისთვის)
            var recentLogs = await _context.TrafficLogs
                .OrderByDescending(t => t.Timestamp)
                .Take(10)
                .ToListAsync();

            // 5. სტატუს კოდების სტატისტიკა (დიაგრამისთვის)
            var statusStats = await _context.TrafficLogs
                .GroupBy(t => t.StatusCode)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Status, v => v.Count);

            // მონაცემების გაგზავნა View-ში ViewBag-ის მეშვეობით
            ViewBag.TotalRequests = totalRequestsToday;
            ViewBag.TotalBlocked = totalBlockedIps;
            ViewBag.AvgLatency = Math.Round(avgLatency, 2);
            ViewBag.RecentLogs = recentLogs;

            // ამას გამოვიყენებთ Pie Chart-ისთვის
            ViewBag.SuccessCount = statusStats.GetValueOrDefault(200, 0);
            ViewBag.ErrorCount = statusStats.Where(x => x.Key >= 400).Sum(x => x.Value);

            return View();
        }
    }
}