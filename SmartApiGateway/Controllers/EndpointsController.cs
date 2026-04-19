using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    // წვდომა აქვს SuperAdmin-ს და Admin-ს
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
            // Include(e => e.CreatedBy) საჭიროა, რომ გავიგოთ ვინ დაამატა
            var endpoints = await _context.ApiEndpoints.Include(e => e.CreatedBy).ToListAsync();
            return View(endpoints);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ApiEndpoint endpoint)
        {
            if (ModelState.IsValid)
            {
                // ვიმახსოვრებთ იმ იუზერის ID-ს, ვინც ამატებს ენდპოინტს
                endpoint.CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier);

                _context.ApiEndpoints.Add(endpoint);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(endpoint);
        }

        // რედაქტირების გვერდის ჩატვირთვა
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var endpoint = await _context.ApiEndpoints.FindAsync(id);
            if (endpoint == null) return NotFound();

            // დაცვა: ჩვეულებრივ ადმინს არ შეუძლია სხვისი ენდპოინტის შეცვლა
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("SuperAdmin") && endpoint.CreatedById != currentUserId)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            return View(endpoint);
        }

        // რედაქტირებული მონაცემების შენახვა
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