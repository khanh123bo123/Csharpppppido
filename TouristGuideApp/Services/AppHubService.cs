using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace TouristGuideApp.Services;

public interface IAppHubService
{
    Task ConnectAsync();
    Task DisconnectAsync();
}

public class AppHubService : IAppHubService
{
    private HubConnection? _hubConnection;
    private readonly HttpClient _httpClient;

    public AppHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task ConnectAsync()
    {
        if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected)
            return;

        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            return;

        var deviceId = DeviceInfo.Current.Idiom.ToString() + "_" + DeviceInfo.Current.Platform.ToString() + "_" + Guid.NewGuid().ToString().Substring(0, 8);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/apphub?deviceId={deviceId}")
            .WithAutomaticReconnect()
            .Build();

        try
        {
            await _hubConnection.StartAsync();
            Debug.WriteLine("SignalR Connected.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SignalR Connection Error: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
