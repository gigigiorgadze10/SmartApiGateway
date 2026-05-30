using Microsoft.AspNetCore.Mvc.Filters;
using SmartApiGateway.Data;
using SmartApiGateway.Models;
using System.Security.Claims;

namespace SmartApiGateway.Filters
{
    public class AuditLogAttribute : ActionFilterAttribute
    {
        private readonly string _actionDescription;

        public AuditLogAttribute(string actionDescription)
        {
            _actionDescription = actionDescription;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var resultContext = await next();

            if (resultContext.Exception == null || resultContext.ExceptionHandled)
            {
                var httpContext = context.HttpContext;
                var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = httpContext.User.Identity?.Name ?? "Unknown";
                var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
                var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Controller = controller,
                    Action = action,
                    Description = _actionDescription,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.UtcNow
                };

                dbContext.AuditLogs.Add(auditLog);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}