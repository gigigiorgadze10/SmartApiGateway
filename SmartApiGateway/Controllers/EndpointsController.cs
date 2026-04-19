using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    // წვდომა აქვთ SuperAdmin-ებს და Admin-ებს
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class EndpointsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EndpointsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // მარშრუტების სია (ავტორებთან ერთად)
        public async Task<IActionResult> Index()
        {
            // .Include(e => e.CreatedBy) კრიტიკულია, რომ ამოვიღოთ იუზერის სახელი
            var endpoints = await _context.ApiEndpoints
                .Include(e => e.CreatedBy)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return View(endpoints);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ApiEndpoint model)
        {
            if (ModelState.IsValid)
            {
                // ვაფიქსირებთ თუ ვინ ამატებს ამ ენდპოინტს
                model.CreatedById = _userManager.GetUserId(User);
                model.CreatedAt = System.DateTime.UtcNow;

                _context.ApiEndpoints.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // რედაქტირების გვერდის ჩატვირთვა
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var endpoint = await _context.ApiEndpoints.FindAsync(id);
            if (endpoint == null) return NotFound();

            return View(endpoint);
        }

        // რედაქტირებული მონაცემების შენახვა
        [HttpPost]
        public async Task<IActionResult> Edit(int id, ApiEndpoint model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var endpointToUpdate = await _context.ApiEndpoints.FindAsync(id);
                if (endpointToUpdate == null) return NotFound();

                // ვაახლებთ მხოლოდ ნებადართულ ველებს
                endpointToUpdate.RoutePath = model.RoutePath;
                endpointToUpdate.TargetUrl = model.TargetUrl;
                endpointToUpdate.Description = model.Description;
                endpointToUpdate.IsActive = model.IsActive;

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
                _context.ApiEndpoints.Remove(endpoint);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}