using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data; // დარწმუნდი, რომ აქ არის ApplicationUser
using SmartApiGateway.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        // მივუთითოთ ზუსტი კლასი SmartApiGateway.Data.ApplicationUser
        private readonly UserManager<SmartApiGateway.Data.ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<SmartApiGateway.Data.ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty, // დაამატე ??
                CurrentRoles = await _userManager.GetRolesAsync(user),
                AvailableRoles = await _roleManager.Roles
        .Select(r => r.Name)
        .Where(name => name != null) // გავფილტროთ null-ები
        .Cast<string>() // დავაკასტოთ string-ზე
        .ToListAsync()
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);

            if (!string.IsNullOrEmpty(model.SelectedRole) && !currentRoles.Contains(model.SelectedRole))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.SelectedRole);
            }

            TempData["Success"] = "მომხმარებლის როლი განახლდა.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                if (user.UserName == User.Identity?.Name)
                {
                    TempData["Error"] = "საკუთარ თავს ვერ წაშლით!";
                    return RedirectToAction(nameof(Index));
                }
                await _userManager.DeleteAsync(user);
                TempData["Success"] = "მომხმარებელი წაიშალა.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}