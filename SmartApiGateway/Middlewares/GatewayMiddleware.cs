using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

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
            // 1. მომხმარებლის (კლიენტის) IP მისამართის წამოღება
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // 2. IP Blacklist-ის სიმულაცია (სატესტო სია)
            // მომავალში ამ სიას შენი PostgreSQL ბაზიდან წამოვიღებთ
            var blacklistedIps = new List<string>
            {
                "192.168.1.100",
                "10.0.0.5" 
                // "::1", // <-- თუ გინდა შენი თავი დაბლოკო ლოკალურად (localhost-ის IPv6 მისამართი), მოხსენი კომენტარი
                // "127.0.0.1" // <-- localhost-ის IPv4 მისამართი
            };

            // 3. ვამოწმებთ, არის თუ არა კლიენტის IP შავ სიაში
            if (blacklistedIps.Contains(clientIp))
            {
                // თუ შავ სიაშია, ვუბრუნებთ 403 სტატუსს და ვწყვეტთ მოთხოვნას
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Access Denied. Your IP address is blacklisted.\"}");

                return; // Pipeline წყდება აქ!
            }

            // 4. თუ IP უსაფრთხოა, ვაგრძელებთ ჩვეულებრივ API Gateway ლოგიკას
            if (context.Request.Path.StartsWithSegments("/api", out var remainingPath))
            {
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

                return;
            }

            await _next(context);
        }
    }
}