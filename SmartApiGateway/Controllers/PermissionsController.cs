using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class PermissionsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}