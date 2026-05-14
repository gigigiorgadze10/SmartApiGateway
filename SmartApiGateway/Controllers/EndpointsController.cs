using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Security.Claims;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin, Admin")]
    public class EndpointsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EndpointsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var query = _context.ApiEndpoints.Include(e => e.CreatedBy).AsQueryable();

            if (!User.IsInRole("SuperAdmin"))
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(e => e.CreatedById == currentUserId);
            }

            var endpoints = await query.ToListAsync();
            return View(endpoints);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ApiEndpoint { RateLimitPerSecond = 5 });
        }

        [HttpPost]
        public async Task<IActionResult> Create(ApiEndpoint endpoint)
        {
            if (ModelState.IsValid)
            {
                endpoint.CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.ApiEndpoints.Add(endpoint);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(endpoint);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var endpoint = await _context.ApiEndpoints.FindAsync(id);
            if (endpoint == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("SuperAdmin") && endpoint.CreatedById != currentUserId)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            return View(endpoint);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, ApiEndpoint model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var endpoint = await _context.ApiEndpoints.FindAsync(id);
                if (endpoint == null) return NotFound();

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("SuperAdmin") && endpoint.CreatedById != currentUserId)
                {
                    return RedirectToAction("AccessDenied", "Home");
                }

                endpoint.RoutePath = model.RoutePath;
                endpoint.TargetUrl = model.TargetUrl;
                endpoint.IsActive = model.IsActive;
                endpoint.Description = model.Description;

                endpoint.EnableRateLimiting = model.EnableRateLimiting;
                endpoint.RateLimitPerSecond = model.RateLimitPerSecond;
                endpoint.EnableSmartShield = model.EnableSmartShield;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var endpoint = await _context.ApiEndpoints.FindAsync(id);
            if (endpoint != null)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!User.IsInRole("SuperAdmin") && endpoint.CreatedById != currentUserId)
                {
                    return RedirectToAction("AccessDenied", "Home");
                }

                _context.ApiEndpoints.Remove(endpoint);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}