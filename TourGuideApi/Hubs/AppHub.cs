using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace TourGuideApi.Hubs;

public class AppHub : Hub
{
    // Static dictionaries to track online users
    // Map ConnectionId to DeviceId or UserId
    private static readonly ConcurrentDictionary<string, string> _connections = new();

    // Tracking how many connections per unique user/device
    private static readonly ConcurrentDictionary<string, int> _userDevices = new();

    public override async Task OnConnectedAsync()
    {
        var source = Context.GetHttpContext()?.Request.Query["source"].ToString();
        var isWeb = source == "web";

        if (!isWeb)
        {
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = "Anonymous_" + Context.ConnectionId;
            }

            _connections.TryAdd(Context.ConnectionId, deviceId);
            _userDevices.AddOrUpdate(deviceId, 1, (key, count) => count + 1);
        }

        await BroadcastOnlineUsers();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var deviceId))
        {
            if (_userDevices.TryGetValue(deviceId, out var count))
            {
                if (count > 1)
                {
                    _userDevices.TryUpdate(deviceId, count - 1, count);
                }
                else
                {
                    _userDevices.TryRemove(deviceId, out _);
                }
            }
        }

        await BroadcastOnlineUsers();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task BroadcastOnlineUsers()
    {
        var totalConnections = _connections.Count;
        var totalUniqueUsers = _userDevices.Count;

        // Broadcast to everyone (including the web dashboard)
        await Clients.All.SendAsync("UpdateOnlineUsers", new
        {
            TotalConnections = totalConnections,
            TotalUniqueUsers = totalUniqueUsers
        });
    }

    // Explicitly ask for current stats
    public async Task GetOnlineUsers()
    {
        await Clients.Caller.SendAsync("UpdateOnlineUsers", new
        {
            TotalConnections = _connections.Count,
            TotalUniqueUsers = _userDevices.Count
        });
    }
}
