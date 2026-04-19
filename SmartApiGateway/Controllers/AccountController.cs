using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartApiGateway.Data;
using SmartApiGateway.ViewModels;

namespace SmartApiGateway.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // ლოგინის გვერდის ჩატვირთვა
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // ლოგინის ფორმის გაგზავნა
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Dashboard", "Home");
                }
                ModelState.AddModelError(string.Empty, "არასწორი ელ-ფოსტა ან პაროლი");
            }
            return View(model);
        }

        // რეგისტრაციის გვერდის ჩატვირთვა
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // რეგისტრაციის ფორმის გაგზავნა
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // რეგისტრაციისას ვანიჭებთ "Admin" როლს (ან სხვა სასურველს)
                    // შეგიძლიათ model-ში დაამატოთ Role ველი და აქ ის გამოიყენოთ
                    await _userManager.AddToRoleAsync(user, "Admin");

                    TempData["Success"] = "მომხმარებელი წარმატებით შეიქმნა!";
                    return RedirectToAction("Index", "Users");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        // სისტემიდან გასვლა
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // ვაგენერირებთ ტოკენს
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                // აქ იდეაში უნდა იგზავნებოდეს მეილი, მაგრამ ტესტირებისთვის პირდაპირ ეკრანზე გამოგვაქვს
                TempData["ResetToken"] = token;
                TempData["ResetEmail"] = email;
                TempData["Message"] = "სატესტო გარემო: გამოიყენეთ ქვემოთ მოცემული ტოკენი პაროლის შესაცვლელად.";
            }
            else
            {
                TempData["Error"] = "მომხმარებელი ამ ელ-ფოსტით არ მოიძებნა.";
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (result.Succeeded)
                {
                    TempData["Success"] = "პაროლი წარმატებით შეიცვალა! შეგიძლიათ შეხვიდეთ სისტემაში.";
                    return RedirectToAction("Login");
                }
            }
            TempData["Error"] = "პაროლის აღდგენა ვერ მოხერხდა. ტოკენი ვადაგასულია ან არასწორია.";
            return RedirectToAction("Login");
        }
    }
}