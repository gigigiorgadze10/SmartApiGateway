using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Middlewares;
using SmartApiGateway.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

builder.Services.AddHttpClient("gateway", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Gateway-Source", "SmartApiGateway");
});

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

builder.Services.AddSingleton<MongoLogService>();
builder.Services.AddHostedService<LogCleanupService>();
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    for (int i = 0; i < 5; i++)
    {
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            logger.LogInformation("ბაზის მიგრაციების შემოწმება...");
            context.Database.Migrate();
            logger.LogInformation("სისტემური მონაცემების სიდირება...");
            await DbSeeder.SeedRolesAndAdminAsync(services);
            logger.LogInformation("ბაზა წარმატებით მომზადდა.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"მცდელობა {i + 1}: შეცდომა ბაზის მომზადებისას. 5 წამში ვცდით ხელახლა...");
            if (i == 4) throw;
            await Task.Delay(5000);
        }
    }
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
var supportedCultures = new[] { "ka", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ka")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);
app.UseRouting();
app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<TrafficHub>("/trafficHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseWhen(context =>
    !context.Request.Path.StartsWithSegments("/trafficHub") &&
    !context.Request.Path.StartsWithSegments("/Home") &&
    !context.Request.Path.StartsWithSegments("/Account") &&
    !context.Request.Path.StartsWithSegments("/Endpoints") &&
    !context.Request.Path.StartsWithSegments("/Users") &&
    !context.Request.Path.StartsWithSegments("/Roles") &&
    !context.Request.Path.StartsWithSegments("/BlockedIps") &&
    !context.Request.Path.StartsWithSegments("/Settings") &&
    !context.Request.Path.StartsWithSegments("/lib") &&
    !context.Request.Path.StartsWithSegments("/css") &&
    !context.Request.Path.StartsWithSegments("/js"),
    appBuilder =>
    {
        appBuilder.UseMiddleware<GatewayMiddleware>();
    });

app.Run();