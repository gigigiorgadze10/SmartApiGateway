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

// 2. Identity კონფიგურაცია
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

// 4. CORS პოლიტიკა - ეს აუცილებელია SignalR-ისთვის ლოკალზეც და სერვერზეც
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true) // ნებას რთავს ნებისმიერ origin-ს (localhost, render და ა.შ.)
              .AllowCredentials(); // აუცილებელია SignalR-ისთვის
    });
});

// 5. IHttpClientFactory
builder.Services.AddHttpClient("gateway", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Gateway-Source", "SmartApiGateway");
});

// 6. Rate Limiting
builder.Services.AddRateLimiter(options =>
{
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
        await ctx.HttpContext.Response.WriteAsync(
            "{\"error\":\"Too Many Requests. გთხოვთ, 1 წუთში სცადოთ.\"}", token);
    };
});

builder.Services.AddHostedService<LogCleanupService>();
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR(); // SignalR-ის რეგისტრაცია

var app = builder.Build();

// მონაცემთა ბაზის ავტომატური მიგრაცია (Render-ზე დაჰოსტვისას ძალიან გამოგადგება)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        if (context.Database.GetPendingMigrations().Any())
        {
            context.Database.Migrate();
        }
        await DbSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "შეცდომა ბაზის მომზადებისას.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ... (ზედა ნაწილი უცვლელია) ...

app.UseRouting();
app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<TrafficHub>("/trafficHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Gateway Middleware - დავამატეთ /Home/FilterData გამონაკლისებში
app.UseWhen(context =>
    !context.Request.Path.StartsWithSegments("/trafficHub") &&
    !context.Request.Path.StartsWithSegments("/Home") &&
    !context.Request.Path.StartsWithSegments("/Account") &&
    !context.Request.Path.StartsWithSegments("/Endpoints") &&
    !context.Request.Path.StartsWithSegments("/Users") &&
    !context.Request.Path.StartsWithSegments("/Roles") &&
    !context.Request.Path.StartsWithSegments("/BlockedIps"),
    appBuilder =>
    {
        appBuilder.UseMiddleware<GatewayMiddleware>();
    });

app.Run();