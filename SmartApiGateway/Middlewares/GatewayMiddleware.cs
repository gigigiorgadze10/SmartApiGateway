using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GatewayMiddleware> _logger;
        private static readonly ConcurrentDictionary<int, int> _rotationIndices = new();

        public GatewayMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<GatewayMiddleware> logger)
        {
            _next = next;
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // თუ მოთხოვნა არის შიდა ფაილებზე (CSS, JS) ან SignalR-ზე, პირდაპირ გავატაროთ
            if (context.Request.Path.StartsWithSegments("/trafficHub") ||
                context.Request.Path.StartsWithSegments("/lib") ||
                context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js"))
            {
                await _next(context);
                return;
            }

            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

                // 1. IP Blacklist
                if (dbContext.BlockedIps.Any(b => b.IpAddress == clientIp))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("Access Denied: IP Blocked");
                    return;
                }

                string requestPath = context.Request.Path.Value?.TrimEnd('/') ?? "";

                // ვეძებთ ენდპოინტს ბაზაში
                var endpoint = dbContext.ApiEndpoints.AsEnumerable().FirstOrDefault(e => e.IsActive &&
                    (requestPath.Equals(e.RoutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                     requestPath.StartsWith(e.RoutePath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)));

                if (endpoint != null)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var availableUrls = endpoint.GetTargetUrls();
                    int targetIndex = _rotationIndices.AddOrUpdate(endpoint.Id, 0, (id, oldIdx) => (oldIdx + 1) % availableUrls.Length);
                    string selectedBaseUrl = availableUrls[targetIndex];

                    string remainingPath = requestPath.Length > endpoint.RoutePath.TrimEnd('/').Length
                        ? requestPath.Substring(endpoint.RoutePath.TrimEnd('/').Length) : "";

                    string targetUrl = $"{selectedBaseUrl.TrimEnd('/')}{remainingPath}{context.Request.QueryString}";

                    // Caching
                    string cacheKey = $"GatewayCache_{targetUrl}";
                    if (context.Request.Method == "GET" && cache.TryGetValue(cacheKey, out string? cachedBody))
                    {
                        context.Response.ContentType = "application/json";
                        context.Response.Headers["X-Cache"] = "HIT";
                        await context.Response.WriteAsync(cachedBody!);
                        await LogAndBroadcast(hubContext, dbContext, clientIp, targetUrl, context.Request.Method, 200, 0);
                        return;
                    }

                    int statusCode = 500;
                    try
                    {
                        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);
                        var httpClient = _httpClientFactory.CreateClient();

                        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                        statusCode = (int)response.StatusCode;

                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = statusCode;
                            context.Response.Headers["X-Cache"] = "MISS";

                            foreach (var header in response.Headers)
                            {
                                if (!context.Response.Headers.ContainsKey(header.Key))
                                    context.Response.Headers[header.Key] = header.Value.ToArray();
                            }
                            foreach (var header in response.Content.Headers)
                            {
                                if (!context.Response.Headers.ContainsKey(header.Key))
                                    context.Response.Headers[header.Key] = header.Value.ToArray();
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Response already started for {TargetUrl}", targetUrl);
                        }

                        var content = await response.Content.ReadAsStringAsync();
                        if (response.IsSuccessStatusCode && context.Request.Method == "GET")
                        {
                            cache.Set(cacheKey, content, TimeSpan.FromSeconds(30));
                        }

                        // Write body. If response has already started, this will continue the output stream.
                        await context.Response.WriteAsync(content);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Forwarding failed for {TargetUrl}", targetUrl);
                        statusCode = 502;
                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = statusCode;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"error\": \"Could not reach target service.\"}");
                        }
                        else
                        {
                            try { context.Abort(); } catch { }
                        }
                    }
                    finally
                    {
                        stopwatch.Stop();
                        await LogAndBroadcast(hubContext, dbContext, clientIp, targetUrl, context.Request.Method, statusCode, stopwatch.ElapsedMilliseconds);
                    }
                    return;
                }
            }

            // თუ ენდპოინტი ბაზაში არ მოიძებნა, გადავცემთ კონტროლერებს (Dashboard, Login და ა.შ.)
            await _next(context);
        }

        private async Task LogAndBroadcast(IHubContext<TrafficHub> hub, ApplicationDbContext db, string ip, string url, string method, int status, long time)
        {
            var log = new TrafficLog { IpAddress = ip, RequestedUrl = url, HttpMethod = method, StatusCode = status, ResponseTimeMs = time, CreatedAt = DateTime.UtcNow };
            db.TrafficLogs.Add(log);
            await db.SaveChangesAsync();

            await hub.Clients.All.SendAsync("ReceiveLog", new
            {
                ipAddress = ip,
                url = url,
                method = method,
                status = status,
                time = time
            });
        }
    }
}