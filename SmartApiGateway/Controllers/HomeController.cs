using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System;
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

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToAction("Dashboard");
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard(string filter = "24h", int limit = 10) // <-- დაემატა limit პარამეტრი
        {
            var query = _context.TrafficLogs.AsQueryable();

            // 1. დროის ფილტრაცია
            if (filter != "all")
            {
                DateTime cutoff = DateTime.UtcNow.AddHours(-24);
                if (filter == "1h") cutoff = DateTime.UtcNow.AddHours(-1);
                else if (filter == "7d") cutoff = DateTime.UtcNow.AddDays(-7);
                else if (filter == "30d") cutoff = DateTime.UtcNow.AddDays(-30);
                else filter = "24h";

                query = query.Where(t => t.CreatedAt >= cutoff);
            }

            // 2. ძირითადი სტატისტიკა
            var totalRequests = await query.CountAsync();
            var avgTime = totalRequests > 0 ? await query.AverageAsync(t => t.ResponseTimeMs) : 0;
            var blockedCount = await _context.BlockedIps.CountAsync();

            // 3. ბოლო მოთხოვნები ლიმიტის გათვალისწინებით (ბრაუზერის დასაცავად All-ის დროს მაქს. 100 ლოგი)
            int logsLimit = limit > 0 ? limit : 100;
            var recentLogs = await query.OrderByDescending(t => t.CreatedAt).Take(logsLimit).ToListAsync();

            // 4. ჩარტის მონაცემები (სტაბილურობისთვის ყოველთვის ბოლო 50 წერტილს ვიღებთ)
            var chartLogs = await query.OrderByDescending(t => t.CreatedAt).Take(50).ToListAsync();
            chartLogs = chartLogs.OrderBy(t => t.CreatedAt).ToList();
            var chartLabels = chartLogs.Select(t => t.CreatedAt.ToLocalTime().ToString("HH:mm")).ToList();
            var chartData = chartLogs.Select(t => t.ResponseTimeMs).ToList();

            // 5. სტატუს კოდები
            var success = await query.CountAsync(t => t.StatusCode >= 200 && t.StatusCode < 300);
            var clientErr = await query.CountAsync(t => t.StatusCode >= 400 && t.StatusCode < 500);
            var serverErr = await query.CountAsync(t => t.StatusCode >= 500);

            // 6. Top 5 IP
            var topIps = await query
                .GroupBy(t => t.IpAddress)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { Ip = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Ip, v => v.Count);

            // 7. ენდპოინტების სტატისტიკა (ლიმიტით)
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

            // თუ limit > 0, ვიღებთ კონკრეტულ რაოდენობას, თუ 0-ია (ანუ All), ამოგვაქვს ყველა
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
                EndpointLimit = limit, // <-- ვაწვდით მოდელს არჩეულ ლიმიტს
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