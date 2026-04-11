using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Models; // დარწმუნდი რომ მოდელები შემოტანილია

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        // IdentityUser-ის ნაცვლად ვიყენებთ ApplicationUser-ს
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // ბაზიდან მოგვაქვს ApplicationUser-ების სია
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                if (user.UserName == User.Identity?.Name)
                {
                    TempData["Error"] = "საკუთარი თავის წაშლა შეუძლებელია!";
                    return RedirectToAction(nameof(Index));
                }
                await _userManager.DeleteAsync(user);
                TempData["Success"] = "მომხმარებელი წაიშალა.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}