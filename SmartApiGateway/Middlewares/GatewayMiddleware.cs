using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using SmartApiGateway.Data;
using SmartApiGateway.Hubs;
using SmartApiGateway.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace SmartApiGateway.Middlewares
{
    public class GatewayMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly ConcurrentDictionary<int, int> _rotationIndices = new();

        // შიდა პანელის მარშრუტები — Gateway-ი ამ prefix-ებს გამოტოვებს
        private static readonly string[] _internalPrefixes =
        {
            "/trafficHub", "/lib", "/css", "/js", "/favicon.ico",
            "/Home", "/Account", "/Endpoints", "/BlockedIps",
            "/Users", "/Roles", "/Settings", "/Permissions"
        };

        public GatewayMiddleware(
            RequestDelegate next,
            IServiceScopeFactory scopeFactory,
            IHttpClientFactory httpClientFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            // თუ მოთხოვნა შიდა პანელზეა ან root-ზე — პირდაპირ გავატაროთ
            if (path == "/" || _internalPrefixes.Any(p =>
                    path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<TrafficHub>>();

            // 1. IP Blacklist — cache-ში ვინახავთ 30 წამი
            if (await IsIpBlockedAsync(dbContext, cache, clientIp))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Access Denied: IP Blocked");
                return;
            }

            // 2. Endpoint lookup — cache-ში ვინახავთ, DB-ზე ყოველ request-ზე არ ვდივართ
            string requestPath = path.TrimEnd('/');
            var endpoint = await FindEndpointAsync(dbContext, cache, requestPath);

            if (endpoint == null)
            {
                // ბაზაში მარშრუტი ვერ მოიძებნა — კონტროლერებს გავატანთ (404 და სხვ.)
                await _next(context);
                return;
            }

            // 3. Load Balancing — Round Robin
            var availableUrls = endpoint.GetTargetUrls();
            int targetIndex = _rotationIndices.AddOrUpdate(
                endpoint.Id, 0, (_, old) => (old + 1) % availableUrls.Length);
            string selectedBaseUrl = availableUrls[targetIndex];

            string suffix = requestPath.Length > endpoint.RoutePath.TrimEnd('/').Length
                ? requestPath.Substring(endpoint.RoutePath.TrimEnd('/').Length)
                : "";
            string targetUrl = $"{selectedBaseUrl.TrimEnd('/')}{suffix}{context.Request.QueryString}";

            // 4. Response Caching — მხოლოდ GET მოთხოვნებისთვის
            string cacheKey = $"GW_{targetUrl}";
            if (HttpMethods.IsGet(context.Request.Method) &&
                cache.TryGetValue(cacheKey, out string? cached))
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.Headers["X-Cache"] = "HIT";
                await context.Response.WriteAsync(cached!);
                await LogAndBroadcastAsync(hub, dbContext, clientIp, targetUrl,
                    context.Request.Method, 200, 0);
                return;
            }

            // 5. Proxy — downstream სერვისზე გაგზავნა
            var stopwatch = Stopwatch.StartNew();
            int statusCode = 500;

            try
            {
                var httpClient = _httpClientFactory.CreateClient("gateway");
                using var requestMessage = BuildProxyRequest(context, targetUrl, clientIp);
                using var response = await httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.RequestAborted);

                statusCode = (int)response.StatusCode;
                context.Response.StatusCode = statusCode;
                context.Response.Headers["X-Cache"] = "MISS";

                // Downstream-ის response headers-ის კოპირება
                foreach (var header in response.Headers)
                {
                    // Transfer-Encoding-ი ASP.NET-მა თავად უნდა განსაზღვროს
                    if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                        continue;
                    context.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
                }

                // Content-Type-ის გადაცემა
                if (response.Content.Headers.ContentType != null)
                    context.Response.ContentType = response.Content.Headers.ContentType.ToString();

                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && HttpMethods.IsGet(context.Request.Method))
                    cache.Set(cacheKey, content, TimeSpan.FromSeconds(30));

                await context.Response.WriteAsync(content);
            }
            catch (TaskCanceledException)
            {
                statusCode = 504;
                context.Response.StatusCode = 504;
                await context.Response.WriteAsync("{\"error\":\"Gateway Timeout\"}");
            }
            catch (Exception ex)
            {
                statusCode = 502;
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync($"{{\"error\":\"Bad Gateway\",\"detail\":\"{ex.Message}\"}}");
            }
            finally
            {
                stopwatch.Stop();
                await LogAndBroadcastAsync(hub, dbContext, clientIp, targetUrl,
                    context.Request.Method, statusCode, stopwatch.ElapsedMilliseconds);
            }
        }

        // =================== Helper Methods ===================

        /// <summary>
        /// Proxy request-ის build — body, headers და forwarding meta-data
        /// </summary>
        private static HttpRequestMessage BuildProxyRequest(
            HttpContext context, string targetUrl, string clientIp)
        {
            var msg = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

            // POST / PUT / PATCH — body-ის კოპირება
            if (!HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) &&
                !HttpMethods.IsDelete(context.Request.Method) &&
                context.Request.ContentLength is > 0)
            {
                msg.Content = new StreamContent(context.Request.Body);
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                {
                    msg.Content.Headers.TryAddWithoutValidation(
                        "Content-Type", context.Request.ContentType);
                }
            }

            // Original request headers-ის კოპირება
            // (Content-* headers msg.Content-ზე გადადის, არა msg.Headers-ზე)
            foreach (var header in context.Request.Headers)
            {
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    continue;
                msg.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            // Gateway meta-headers
            msg.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
            msg.Headers.TryAddWithoutValidation(
                "X-Forwarded-Host", context.Request.Host.ToString());
            msg.Headers.TryAddWithoutValidation("X-Gateway-Version", "SmartGateway/1.0");

            return msg;
        }

        /// <summary>
        /// IP Blacklist — cache-ით (30 წამი) — DB-ზე ყოველ request-ზე არ ვდივართ
        /// </summary>
        private static async Task<bool> IsIpBlockedAsync(
            ApplicationDbContext db, IMemoryCache cache, string ip)
        {
            const string key = "blocked_ips_set";
            if (!cache.TryGetValue(key, out HashSet<string>? set))
            {
                var ips = await db.BlockedIps
                    .Select(b => b.IpAddress)
                    .ToListAsync();
                set = new HashSet<string>(ips, StringComparer.OrdinalIgnoreCase);
                cache.Set(key, set, TimeSpan.FromSeconds(30));
            }
            return set!.Contains(ip);
        }

        /// <summary>
        /// Endpoint lookup — cache-ით (30 წამი) — AsEnumerable()-ის ნაცვლად
        /// </summary>
        private static async Task<ApiEndpoint?> FindEndpointAsync(
            ApplicationDbContext db, IMemoryCache cache, string requestPath)
        {
            const string key = "active_endpoints";
            if (!cache.TryGetValue(key, out List<ApiEndpoint>? endpoints))
            {
                endpoints = await db.ApiEndpoints
                    .Where(e => e.IsActive)
                    .ToListAsync();
                cache.Set(key, endpoints, TimeSpan.FromSeconds(30));
            }

            return endpoints!.FirstOrDefault(e =>
                requestPath.Equals(
                    e.RoutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                requestPath.StartsWith(
                    e.RoutePath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Traffic log-ის შენახვა DB-ში და SignalR-ით broadcast-ი
        /// </summary>
        private static async Task LogAndBroadcastAsync(
    IHubContext<TrafficHub> hub,
    ApplicationDbContext db,
    string ip, string url, string method, int status, long time)
        {
            var log = new TrafficLog
            {
                IpAddress = ip,
                RequestedUrl = url,
                HttpMethod = method,
                StatusCode = status,
                ResponseTimeMs = time,
                CreatedAt = DateTime.UtcNow
            };

            db.TrafficLogs.Add(log);
            await db.SaveChangesAsync();

            // 1. სიგნალი ლოგების ცხრილისთვის (თუ გაქვს ასეთი)
            await hub.Clients.All.SendAsync("ReceiveLog", new
            {
                ipAddress = ip,
                url,
                method,
                status,
                time
            });

            // 2. კრიტიკული შესწორება: სიგნალი დეშბორდისთვის!
            // Dashboard.cshtml სწორედ ამ სახელს ელოდება
            await hub.Clients.All.SendAsync("ReceiveTrafficUpdate");
        }
    }
}