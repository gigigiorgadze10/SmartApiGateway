using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartApiGateway.ViewModels;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin")] // მხოლოდ სუპერ ადმინს შეუძლია მართვა
    public class PermissionsController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public PermissionsController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Manage(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null) return NotFound();

            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var model = new ManageRolePermissionsViewModel { RoleId = roleId, RoleName = role.Name ?? string.Empty };

            // აქ ვწერთ სისტემაში არსებულ ყველა შესაძლო უფლებას (უფრო მეტის დამატებაც შეგიძლია)
            var allPermissions = new[] { "ViewDashboard", "ManageEndpoints", "ManageRoles", "ManageUsers", "ViewLogs" };

            foreach (var permission in allPermissions)
            {
                model.RoleClaims.Add(new RoleClaimViewModel
                {
                    Type = "Permission",
                    Value = permission,
                    Selected = existingClaims.Any(c => c.Value == permission)
                });
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Manage(ManageRolePermissionsViewModel model)
        {
            var role = await _roleManager.FindByIdAsync(model.RoleId);
            if (role == null) return NotFound();

            var claims = await _roleManager.GetClaimsAsync(role);

            // ვშლით ძველ უფლებებს
            foreach (var claim in claims)
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            // ვამატებთ ახალ მონიშნულ უფლებებს
            var selectedClaims = model.RoleClaims.Where(c => c.Selected).ToList();
            foreach (var claim in selectedClaims)
            {
                await _roleManager.AddClaimAsync(role, new Claim(claim.Type, claim.Value));
            }

            return RedirectToAction("Index", "Roles");
        }
    }
}