using Microsoft.AspNetCore.Identity;

namespace SmartApiGateway.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // ვიძახებთ როლებისა და მომხმარებლების მენეჯერებს
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. სისტემური როლების შექმნა
            string[] roleNames = { "SuperAdmin", "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. მთავარი ადმინისტრატორის (SuperAdmin) შექმნა
            string adminEmail = "admin@gateway.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "მთავარი",
                    LastName = "ადმინისტრატორი",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                // ვქმნით მომხმარებელს და ვადებთ პაროლს (პაროლი: admin123)
                var createPowerUser = await userManager.CreateAsync(newAdmin, "admin123");

                if (createPowerUser.Succeeded)
                {
                    // ვანიჭებთ SuperAdmin როლს
                    await userManager.AddToRoleAsync(newAdmin, "SuperAdmin");
                }
            }
        }
    }
}