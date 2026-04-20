using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Middlewares;
using SmartApiGateway.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL კავშირი
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Identity კონფიგურაცია — password policy განახლდა
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Cookie / Access Denied კონფიგურაცია
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// 4. IHttpClientFactory — GatewayMiddleware-ისთვის
//    (new HttpClient()-ის ნაცვლად socket exhaustion-ის თავიდან ასაცილებლად)
builder.Services.AddHttpClient("gateway", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Gateway-Source", "SmartApiGateway");
});

// 5. Rate Limiting — per-endpoint; 429 Too Many Requests-ს დააბრუნებს
builder.Services.AddRateLimiter(options =>
{
    // Fixed Window: 1 წუთში მაქსიმუმ 200 მოთხოვნა
    options.AddFixedWindowLimiter("gateway_limit", cfg =>
    {
        cfg.PermitLimit = 200;
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 0;
    });

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"error\":\"Too Many Requests. გთხოვთ, 1 წუთში სცადოთ.\"}", token);
    };
});

// 6. Background Service — ძველი log-ების ავტომატური გასუფთავება
builder.Services.AddHostedService<LogCleanupService>();

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

// Rate Limiter უნდა იყოს Authentication-ამდე
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// 1. კონტროლერების მარშრუტები (Dashboard, Login და სხვ.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 2. SignalR Hub
app.MapHub<TrafficHub>("/trafficHub");

// 3. Gateway Middleware — ბოლოს, რომ შიდა მარშრუტები არ "დაბლოკოს"
app.UseMiddleware<GatewayMiddleware>();

// მონაცემთა ბაზის Seeding
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
        logger.LogError(ex, "შეცდომა ბაზაში მონაცემების ჩაწერისას (Seeding).");
    }
}

app.Run();