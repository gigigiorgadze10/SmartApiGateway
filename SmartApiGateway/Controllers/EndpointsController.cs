using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin")] // მხოლოდ სუპერ ადმინისთვის
    public class EndpointsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EndpointsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. გამოაქვს ყველა მარშრუტი
        public IActionResult Index()
        {
            var endpoints = _context.ApiEndpoints.OrderByDescending(e => e.CreatedAt).ToList();
            return View(endpoints);
        }

        // 2. დამატების გვერდის გახსნა
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 3. დამატების შენახვა ბაზაში
        [HttpPost]
        public async Task<IActionResult> Create(ApiEndpoint endpoint)
        {
            if (ModelState.IsValid)
            {
                _context.ApiEndpoints.Add(endpoint);
                await _context.SaveChangesAsync();
                TempData["Success"] = "ენდპოინტი წარმატებით დაემატა!";
                return RedirectToAction(nameof(Index));
            }
            return View(endpoint);
        }

        // 4. წაშლის ფუნქცია
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var endpoint = await _context.ApiEndpoints.FindAsync(id);
            if (endpoint != null)
            {
                _context.ApiEndpoints.Remove(endpoint);
                await _context.SaveChangesAsync();
                TempData["Success"] = "ენდპოინტი წაიშალა.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}