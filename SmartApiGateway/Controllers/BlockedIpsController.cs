using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin, Admin")]
    public class BlockedIpsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlockedIpsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var blocked = _context.BlockedIps.ToList();
            return View(blocked);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string ipAddress, string reason)
        {
            if (!string.IsNullOrEmpty(ipAddress))
            {
                var block = new BlockedIp { IpAddress = ipAddress, Reason = reason };
                _context.BlockedIps.Add(block);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var ip = await _context.BlockedIps.FindAsync(id);
            if (ip != null)
            {
                _context.BlockedIps.Remove(ip);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}