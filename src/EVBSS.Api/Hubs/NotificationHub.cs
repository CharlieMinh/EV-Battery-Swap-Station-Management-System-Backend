using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EVBSS.Api.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                // Add the user to a group for their user ID, so we can send them notifications directly.
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);

                // Add user to a group based on their role
                if (Context.User.IsInRole("Admin"))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                }
                else if (Context.User.IsInRole("Staff"))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Staff");
                    // TODO: Add staff to a station-specific group if needed in the future
                    // var stationId = GetStationIdForStaff(userId);
                    // if(stationId != null) {
                    //     await Groups.AddToGroupAsync(Context.ConnectionId, $"Station_{stationId}");
                    // }
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

                if (Context.User.IsInRole("Admin"))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
                }
                else if (Context.User.IsInRole("Staff"))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Staff");
                }
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}