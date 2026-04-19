using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    // მხოლოდ SuperAdmin-ს აქვს ამ კონტროლერზე წვდომა!
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // მომხმარებლების სია
        public IActionResult Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // რედაქტირების გვერდის ჩატვირთვა
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var currentRole = userRoles.FirstOrDefault();

            var roles = _roleManager.Roles.Select(r => new SelectListItem
            {
                Value = r.Name,
                Text = r.Name,
                Selected = r.Name == currentRole
            }).ToList();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                SelectedRole = currentRole,
                AvailableRoles = roles
            };

            return View(model);
        }

        // მონაცემების შენახვა
        [HttpPost]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                // თუ ვალიდაცია ვერ გაიარა, როლების სია ისევ უნდა ჩავუტვირთოთ
                model.AvailableRoles = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
                return View(model);
            }

            // 1. ვაახლებთ მხოლოდ UserName-ს (Email და Password ხელშეუხებელია შენი მოთხოვნის მიხედვით)
            user.UserName = model.UserName;
            await _userManager.UpdateAsync(user);

            // 2. როლის განახლების ლოგიკა
            if (!string.IsNullOrEmpty(model.SelectedRole))
            {
                var currentRoles = await _userManager.GetRolesAsync(user);

                // თუ როლი შეიცვალა
                if (!currentRoles.Contains(model.SelectedRole))
                {
                    // ვშლით ძველ როლებს
                    if (currentRoles.Any())
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    // ვანიჭებთ ახალ როლს
                    await _userManager.AddToRoleAsync(user, model.SelectedRole);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // მომხმარებლის წაშლა
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // დაცვა: საკუთარ თავს ვერ წაშლის SuperAdmin
                if (user.UserName == User.Identity?.Name)
                {
                    TempData["Error"] = "საკუთარი თავის წაშლა შეუძლებელია!";
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.DeleteAsync(user);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}