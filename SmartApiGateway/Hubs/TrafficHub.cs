using Microsoft.AspNetCore.SignalR;

namespace SmartApiGateway.Hubs
{
    public class TrafficHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (Context.User != null && Context.User.IsInRole("SuperAdmin"))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "SuperAdmins");
            }

            await base.OnConnectedAsync();
        }
    }
}