using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Middlewares;
using SmartApiGateway.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL კავშირი
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity კონფიგურაცია
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// --- კრიტიკული ცვლილება რიგითობაში ---
// 1. ჯერ ვარეგისტრირებთ სტანდარტულ გვერდებს (Home, Dashboard, Login)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<TrafficHub>("/trafficHub");

// 2. მხოლოდ ამის შემდეგ ვრთავთ Gateway-ს, რომ შიდა მარშრუტები არ "დაბლოკოს"
app.UseMiddleware<GatewayMiddleware>();

// მონაცემთა ბაზის Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try { await DbSeeder.SeedRolesAndAdminAsync(services); }
    catch (Exception)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError("შეცდომა ბაზაში მონაცემების ჩაწერისას (Seeding).");
    }
}

app.Run();