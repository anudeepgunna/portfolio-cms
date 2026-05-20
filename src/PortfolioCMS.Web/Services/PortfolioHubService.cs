using Microsoft.AspNetCore.SignalR.Client;

namespace PortfolioCMS.Web.Services;

/// <summary>
/// Connects to the SignalR hub on the API and broadcasts
/// live content/theme updates to any subscribed Blazor component.
/// </summary>
public class PortfolioHubService : IAsyncDisposable
{
    private HubConnection? _hub;

    // Events that Blazor components subscribe to
    public event Action<string, object?>? OnContentUpdated;
    public event Action<ThemeDto?>? OnThemeUpdated;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task StartAsync(string apiBaseUrl)
    {
        if (_hub is not null) return;

        _hub = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl}/hubs/portfolio")
            .WithAutomaticReconnect()    // reconnects on network blip
            .Build();

        // Wire up server → client events
        _hub.On<ContentUpdatePayload>("ContentUpdated", payload =>
            OnContentUpdated?.Invoke(payload.SectionType, payload.Payload));

        _hub.On<ThemeDto>("ThemeUpdated", theme =>
            OnThemeUpdated?.Invoke(theme));

        await _hub.StartAsync();

        // Join the "viewers" group to receive broadcasts
        await _hub.InvokeAsync("JoinViewerGroup");
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    private record ContentUpdatePayload(string SectionType, object? Payload);
}
