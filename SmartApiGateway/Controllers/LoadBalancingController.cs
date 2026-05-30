using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartApiGateway.Controllers
{
    [Authorize(Roles = "SuperAdmin, Admin")]
    public class LoadBalancingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}