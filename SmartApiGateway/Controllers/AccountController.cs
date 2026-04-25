using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartApiGateway.Data;
using SmartApiGateway.ViewModels;
using System.Net;

namespace SmartApiGateway.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // =================== Login ===================

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Dashboard", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // 1. ვპოულობთ იუზერს იმეილით
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                // 2. ვიყენებთ ნაპოვნი იუზერის UserName-ს (იქნება ეს იმეილი თუ "adminovich") 
                // და ვამოწმებთ პაროლს
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName!,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return RedirectToAction("Dashboard", "Home");
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "ანგარიში დაბლოკილია ზედმეტი მცდელობების გამო.");
                    return View(model);
                }
            }

            // ზოგადი შეცდომა უსაფრთხოებისთვის
            ModelState.AddModelError(string.Empty, "არასწორი ელ-ფოსტა ან პაროლი.");
            return View(model);
        }

        // =================== Register ===================

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email, // რეგისტრაციისას UserName და Email ერთი და იგივეა
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = true // სიდერის მსგავსად, ავტომატურად ვადასტურებთ
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                TempData["Success"] = "მომხმარებელი წარმატებით შეიქმნა!";
                return RedirectToAction("Login", "Account");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // =================== Logout ===================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        // =================== Forgot/Reset Password ===================

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            TempData["Message"] = "თუ ეს ელ-ფოსტა სისტემაში გვხვდება, მიიღებთ შეტყობინებას.";
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return View();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account",
                new { email, token = WebUtility.UrlEncode(token) }, Request.Scheme);

            if (HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                TempData["DevResetLink"] = resetLink;
            }
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ResetPasswordAsync(user, WebUtility.UrlDecode(token), newPassword);
            if (result.Succeeded)
            {
                TempData["Success"] = "პაროლი შეიცვალა!";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View();
        }
    }
}