using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartApiGateway.Data;
using SmartApiGateway.Models; // მოდელების შემოტანა
using System.Linq;

namespace SmartApiGateway.Controllers
{
    [Authorize] // დაცულია, მხოლოდ დალოგინებულებისთვის
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        // ბაზის კონტექსტის შემოტანა
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var allLogs = _context.TrafficLogs.ToList();

            // სტატისტიკის დათვლა View-სთვის
            ViewBag.TotalRequests = allLogs.Count;
            ViewBag.AvgLatency = allLogs.Any() ? Math.Round(allLogs.Average(l => l.ResponseTimeMs)) : 0;
            ViewBag.BlockedCount = _context.BlockedIps.Count();
            ViewBag.ErrorsCount = allLogs.Count(l => l.StatusCode >= 400); // ერორების რაოდენობა

            // ბოლო 20 ლოგის წამოღება ცხრილისთვის
            var recentLogs = allLogs.OrderByDescending(t => t.CreatedAt).Take(20).ToList();

            return View(recentLogs);
        }
    }
}