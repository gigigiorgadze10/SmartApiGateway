using Hangfire.Dashboard;

public class HangfireCustomAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity != null && httpContext.User.Identity.IsAuthenticated && httpContext.User.IsInRole("SuperAdmin");
    }
}