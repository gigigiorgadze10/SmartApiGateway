using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SmartApiGateway.Middlewares
{
    public class GatewayMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _scopeFactory;

        // ინდექსების შესანახი Load Balancer-ისთვის (Round Robin)
        private static readonly ConcurrentDictionary<int, int> _rotationIndices = new();

        public GatewayMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _httpClient = new HttpClient();
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

                // 1. IP Blacklist შემოწმება
                if (dbContext.BlockedIps.Any(b => b.IpAddress == clientIp))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Access Denied: IP Blocked");
                    return;
                }

                // 2. დინამიური მარშრუტიზაცია
                string requestPath = context.Request.Path.Value?.TrimEnd('/') ?? "";
                var endpoint = dbContext.ApiEndpoints.AsEnumerable().FirstOrDefault(e => e.IsActive &&
                    (requestPath.Equals(e.RoutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                     requestPath.StartsWith(e.RoutePath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)));

                if (endpoint != null)
                {
                    var stopwatch = Stopwatch.StartNew();

                    // --- LOAD BALANCER (Round Robin) ---
                    var availableUrls = endpoint.GetTargetUrls();
                    int targetIndex = _rotationIndices.AddOrUpdate(endpoint.Id, 0, (id, oldIdx) => (oldIdx + 1) % availableUrls.Length);
                    string selectedBaseUrl = availableUrls[targetIndex];

                    string remainingPath = requestPath.Length > endpoint.RoutePath.TrimEnd('/').Length
                        ? requestPath.Substring(endpoint.RoutePath.TrimEnd('/').Length) : "";

                    string targetUrl = $"{selectedBaseUrl.TrimEnd('/')}{remainingPath}{context.Request.QueryString}";

                    string cacheKey = $"GatewayCache_{targetUrl}";
                    if (context.Request.Method == "GET" && cache.TryGetValue(cacheKey, out string? cachedBody)) // დაამატე ?
                    {
                        if (cachedBody != null) // შემოწმება
                        {
                            context.Response.ContentType = "application/json";
                            context.Response.Headers["X-Cache"] = "HIT"; // გამოიყენე [] .Add-ის ნაცვლად
                            await context.Response.WriteAsync(cachedBody);
                            await LogAndBroadcast(hubContext, dbContext, clientIp, targetUrl, context.Request.Method, 200, 0);
                            return;
                        }

                        int statusCode = 500;
                        try
                        {
                            var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);
                            foreach (var header in context.Request.Headers)
                            {
                                if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                            }

                            using var response = await _httpClient.SendAsync(requestMessage);
                            statusCode = (int)response.StatusCode;
                            context.Response.StatusCode = statusCode;
                            context.Response.Headers["X-Cache"] = "MISS";

                            var content = await response.Content.ReadAsStringAsync();

                            // თუ წარმატებული GET-ია, ვინახავთ ქეშში 30 წამით
                            if (response.IsSuccessStatusCode && context.Request.Method == "GET")
                            {
                                cache.Set(cacheKey, content, TimeSpan.FromSeconds(30));
                            }

                            await context.Response.WriteAsync(content);
                        }
                        catch { statusCode = 502; }
                        finally
                        {
                            stopwatch.Stop();
                            await LogAndBroadcast(hubContext, dbContext, clientIp, targetUrl, context.Request.Method, statusCode, stopwatch.ElapsedMilliseconds);
                        }
                        return;
                    }
                }
                await _next(context);
            }
        }

        private async Task LogAndBroadcast(IHubContext<TrafficHub> hub, ApplicationDbContext db, string ip, string url, string method, int status, long time)
        {
            var log = new TrafficLog { IpAddress = ip, RequestedUrl = url, HttpMethod = method, StatusCode = status, ResponseTimeMs = time, CreatedAt = DateTime.UtcNow };
            db.TrafficLogs.Add(log);
            await db.SaveChangesAsync();

            await hub.Clients.All.SendAsync("ReceiveLog", new
            {
                ipAddress = log.IpAddress,
                url = log.RequestedUrl,
                method = log.HttpMethod,
                status = log.StatusCode,
                time = log.ResponseTimeMs
            });
        }
    }
}