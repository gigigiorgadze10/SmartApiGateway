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

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return RedirectToAction("Dashboard", "Home");

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
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                TempData["Success"] = "მომხმარებელი წარმატებით შეიქმნა!";
                return RedirectToAction("Index", "Users");
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

        // =================== Forgot Password ===================

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            // უსაფრთხოებისთვის: ყოველთვის ერთი და იგივე მესიჯი ვაჩვენოთ,
            // რომ attacker-მა ვერ გაიგოს, ამ email-ზე user გვყავს თუ არა.
            TempData["Message"] = "თუ ეს ელ-ფოსტა სისტემაში გვხვდება, მიიღებთ შეტყობინებას.";

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return View();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            // Reset ბმული — production-ში ეს EMAIL-ით უნდა გაიგზავნოს (SendGrid, SMTP და სხვ.)
            var resetLink = Url.Action(
                "ResetPassword", "Account",
                new { email, token = encodedToken },
                Request.Scheme);

            // TODO: production-ში ჩაანაცვლეთ email-ის გაგზავნით:
            // await _emailService.SendPasswordResetEmailAsync(email, resetLink!);

            // dev/test გარემოსთვის: ბმულს ვაჩვენებთ (token-ს პირდაპირ არ ვაჩვენებთ)
            if (HttpContext.RequestServices
                    .GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                TempData["DevResetLink"] = resetLink;
                TempData["DevResetEmail"] = email;
            }

            return View();
        }

        // =================== Reset Password ===================

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            string email, string token, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["Error"] = "პაროლის აღდგენა ვერ მოხერხდა.";
                return RedirectToAction("Login");
            }

            // URL encoded token-ის decode
            var decodedToken = WebUtility.UrlDecode(token);
            var result = await _userManager.ResetPasswordAsync(user, decodedToken, newPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "პაროლი წარმატებით შეიცვალა! შეგიძლიათ შეხვიდეთ სისტემაში.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            TempData["Error"] = "პაროლის აღდგენა ვერ მოხერხდა. ბმული ვადაგასულია ან პაროლი სუსტია.";
            return RedirectToAction("Login");
        }
    }
}