using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory; // <-- დაემატა მეხსიერების ბიბლიოთეკა
using SmartApiGateway.Data;
using SmartApiGateway.Models;

namespace SmartApiGateway.Middlewares
{
    public class GatewayMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HttpClient _httpClient;

        public GatewayMiddleware(RequestDelegate next)
        {
            _next = next;
            _httpClient = new HttpClient();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>(); // ვიღებთ Cache სერვისს

            // 1. IP Blacklist შემოწმება
            bool isBlocked = dbContext.BlockedIps.Any(b => b.IpAddress == clientIp);
            if (isBlocked)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Access Denied. Your IP address is blacklisted.\"}");
                return;
            }

            // 2. RATE LIMITING (მაქსიმუმ 100 მოთხოვნა 1 წუთში თითო IP-დან)
            string rateLimitKey = $"RateLimit_{clientIp}";
            int maxRequests = 100;

            if (cache.TryGetValue(rateLimitKey, out int requestCount))
            {
                if (requestCount >= maxRequests)
                {
                    // თუ ლიმიტს გადააჭარბა, ვბლოკავთ (Status 429 Too Many Requests)
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\": \"Too Many Requests. Please try again in a minute.\"}");
                    return;
                }
                cache.Set(rateLimitKey, requestCount + 1, TimeSpan.FromMinutes(1));
            }
            else
            {
                cache.Set(rateLimitKey, 1, TimeSpan.FromMinutes(1));
            }

            // 3. API მარშრუტიზაცია (Reverse Proxy) და ლოგირება
            if (context.Request.Path.StartsWithSegments("/api", out var remainingPath))
            {
                var stopwatch = Stopwatch.StartNew();
                string targetBaseUrl = "https://jsonplaceholder.typicode.com";
                string targetUrl = $"{targetBaseUrl}{remainingPath}{context.Request.QueryString}";

                var targetRequestMessage = new HttpRequestMessage()
                {
                    RequestUri = new Uri(targetUrl),
                    Method = new HttpMethod(context.Request.Method)
                };

                using var responseMessage = await _httpClient.SendAsync(targetRequestMessage);
                context.Response.StatusCode = (int)responseMessage.StatusCode;

                foreach (var header in responseMessage.Content.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }

                await responseMessage.Content.CopyToAsync(context.Response.Body);
                stopwatch.Stop();

                var log = new TrafficLog
                {
                    IpAddress = clientIp,
                    RequestedUrl = targetUrl,
                    HttpMethod = context.Request.Method,
                    StatusCode = (int)responseMessage.StatusCode,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.TrafficLogs.Add(log);
                await dbContext.SaveChangesAsync();
                return;
            }

            await _next(context);
        }
    }
}