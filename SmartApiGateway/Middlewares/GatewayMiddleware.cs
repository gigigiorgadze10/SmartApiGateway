using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using SmartApiGateway.Data;
using SmartApiGateway.Models;

namespace SmartApiGateway.Middlewares
{
    public class GatewayMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;

        public GatewayMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _httpClient = new HttpClient();
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // 1. ვქმნით Scope-ს ბაზასთან სამუშაოდ
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

                // IP Blacklist შემოწმება
                bool isBlocked = dbContext.BlockedIps.Any(b => b.IpAddress == clientIp);
                if (isBlocked)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Access Denied: IP Blocked");
                    return;
                }

                // Rate Limiting
                string rateLimitKey = $"RL_{clientIp}";
                if (cache.TryGetValue(rateLimitKey, out int count) && count >= 100)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Rate limit exceeded");
                    return;
                }
                cache.Set(rateLimitKey, count + 1, TimeSpan.FromMinutes(1));

                // 2. API დინამიური მარშრუტიზაცია
                string requestPath = context.Request.Path.Value?.TrimEnd('/') ?? "";
                var endpoint = dbContext.ApiEndpoints
                    .AsEnumerable()
                    .FirstOrDefault(e => e.IsActive &&
                        (requestPath.Equals(e.RoutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                         requestPath.StartsWith(e.RoutePath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)));

                if (endpoint != null)
                {
                    var stopwatch = Stopwatch.StartNew();
                    string cleanRoute = endpoint.RoutePath.TrimEnd('/');
                    string remaining = requestPath.Length > cleanRoute.Length ? requestPath.Substring(cleanRoute.Length) : "";
                    string targetUrl = $"{endpoint.TargetUrl.TrimEnd('/')}{remaining}{context.Request.QueryString}";

                    int responseStatusCode = 500;
                    try
                    {
                        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);
                        foreach (var header in context.Request.Headers)
                        {
                            if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                        }

                        using var response = await _httpClient.SendAsync(requestMessage);
                        responseStatusCode = (int)response.StatusCode;
                        context.Response.StatusCode = responseStatusCode;

                        foreach (var header in response.Headers)
                            context.Response.Headers[header.Key] = header.Value.ToArray();
                        foreach (var header in response.Content.Headers)
                            context.Response.Headers[header.Key] = header.Value.ToArray();

                        await response.Content.CopyToAsync(context.Response.Body);
                    }
                    catch (Exception ex)
                    {
                        responseStatusCode = 502;
                        context.Response.StatusCode = 502;
                        await context.Response.WriteAsync("Gateway Error: " + ex.Message);
                    }
                    finally
                    {
                        stopwatch.Stop();

                        // 3. ლოგირება (ვიყენებთ ახალ Scope-ს შენახვისთვის)
                        using (var logScope = _scopeFactory.CreateScope())
                        {
                            var logContext = logScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            var log = new TrafficLog
                            {
                                IpAddress = clientIp,
                                RequestedUrl = targetUrl,
                                HttpMethod = context.Request.Method,
                                StatusCode = responseStatusCode,
                                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                                CreatedAt = DateTime.UtcNow
                            };
                            logContext.TrafficLogs.Add(log);
                            await logContext.SaveChangesAsync();
                        }
                    }
                    return;
                }
            }

            await _next(context);
        }
    }
}