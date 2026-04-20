using Microsoft.AspNetCore.Identity;

namespace SmartApiGateway.Data
{
    public static class DbSeeder
    {
        // Seed პაროლი — appsettings.json-ში ან Environment Variable-ში გიჯობია production-ში
        private const string AdminEmail = "admin@gateway.com";
        private const string AdminPassword = "Admin@2024!"; // RequireDigit + RequireUppercase

        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. სისტემური როლების შექმნა (თუ არ არსებობს)
            string[] roleNames = { "SuperAdmin", "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
            }

            // 2. SuperAdmin-ის შექმნა (თუ არ არსებობს)
            var adminUser = await userManager.FindByEmailAsync(AdminEmail);
            if (adminUser != null) return;

            var newAdmin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                FirstName = "მთავარი",
                LastName = "ადმინისტრატორი",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(newAdmin, AdminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, "SuperAdmin");

                var logger = serviceProvider.GetService<ILogger<ApplicationUser>>();
                logger?.LogInformation(
                    "SuperAdmin შეიქმნა: {Email}", AdminEmail);
            }
            else
            {
                var logger = serviceProvider.GetService<ILogger<ApplicationUser>>();
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger?.LogError(
                    "SuperAdmin-ის შექმნა ვერ მოხერხდა: {Errors}", errors);
            }
        }
    }
}