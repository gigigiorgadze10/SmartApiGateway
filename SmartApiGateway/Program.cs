using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Middlewares; // დავამატეთ ჩვენი საქაღალდის მისამართი

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL კავშირის რეგისტრაცია
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity სერვისის დამატება ავტორიზაციისა და როლებისთვის
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. ქუქიების კონფიგურაცია (არაავტორიზებული მომხმარებლების გადასამისამართებლად ლოგინზე)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache(); // ვრთავთ დროებით მეხსიერებას Rate Limiting-ისთვის

var app = builder.Build();

// 4. HTTP მოთხოვნების დამუშავების კონფიგურაცია
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ==========================================================
// ---> აქ ვსვამთ ჩვენს API Gateway Middleware-ს <---
// ==========================================================
app.UseMiddleware<GatewayMiddleware>();

// 5. უსაფრთხოება: აუცილებელია ჯერ Authentication მოხდეს, მერე Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 6. მონაცემთა ბაზის ინიციალიზაცია (Seeding - როლების და პირველი ადმინის შექმნა)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "შეცდომა ბაზაში მონაცემების ჩაწერისას.");
    }
}

app.Run();