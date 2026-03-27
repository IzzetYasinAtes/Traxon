using Microsoft.AspNetCore.SignalR;

namespace Traxon.CryptoTrader.Dashboard.Hubs;

public sealed class TradingHub : Hub<ICryptoHubClient>
{
    public async Task SubscribeToSymbol(string symbol)
        => await Groups.AddToGroupAsync(Context.ConnectionId, symbol);

    public async Task UnsubscribeFromSymbol(string symbol)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol);
}
