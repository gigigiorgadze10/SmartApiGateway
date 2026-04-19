using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    // წვდომა აქვთ SuperAdmin-ს და Admin-ს
    [Authorize(Roles = "SuperAdmin, Admin")]
    public class BlockedIpsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlockedIpsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. მომაქვს ყველა დაბლოკილი IP თავის "ავტორთან" ერთად
            var blockedIps = await _context.BlockedIps
                .Include(b => b.BlockedBy)
                .OrderByDescending(b => b.BlockedAt)
                .ToListAsync();

            // 2. ვითვლით სტატისტიკას: თითოეულმა IP-მ საერთო ჯამში რამდენჯერ შემოაღწია/სცადა
            var trafficCounts = await _context.TrafficLogs
                .GroupBy(t => t.IpAddress)
                .Select(g => new { Ip = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Ip, v => v.Count);

            // გადავცემთ ვიუს
            ViewBag.TrafficCounts = trafficCounts;

            return View(blockedIps);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string ipAddress, string reason)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return RedirectToAction(nameof(Index));

            // ვამოწმებთ, უკვე ხომ არაა დაბლოკილი
            bool exists = await _context.BlockedIps.AnyAsync(b => b.IpAddress == ipAddress);
            if (!exists)
            {
                var blockedIp = new BlockedIp
                {
                    IpAddress = ipAddress.Trim(),
                    Reason = reason,
                    // ვიმახსოვრებთ ვინ დაბლოკა
                    BlockedById = User.FindFirstValue(ClaimTypes.NameIdentifier)
                };

                _context.BlockedIps.Add(blockedIp);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var blockedIp = await _context.BlockedIps.FindAsync(id);
            if (blockedIp != null)
            {
                // დაცვა: თუ სუპერ ადმინი არ ხარ, სხვის დაბლოკილს ვერ მოხსნი
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("SuperAdmin") && blockedIp.BlockedById != currentUserId)
                {
                    return RedirectToAction("AccessDenied", "Home");
                }

                _context.BlockedIps.Remove(blockedIp);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}