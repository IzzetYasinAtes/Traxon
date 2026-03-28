using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.WebSocket;

public sealed class PolymarketWebSocketClient : IAsyncDisposable
{
    private readonly PolymarketOptions              _options;
    private readonly ILogger<PolymarketWebSocketClient> _logger;

    private ClientWebSocket?           _webSocket;
    private CancellationTokenSource?   _cts;
    private Task?                      _receiveTask;
    private IReadOnlyList<string>      _subscribedTokenIds = [];

    private const int ReconnectDelaySeconds = 5;

    /// <summary>Fiyat güncellemesi geldiğinde çağrılır: (tokenId, price)</summary>
    public Func<string, decimal, Task>? OnPriceUpdate { get; set; }

    public PolymarketWebSocketClient(
        IOptions<PolymarketOptions> options,
        ILogger<PolymarketWebSocketClient> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    /// <summary>WebSocket bağlantısını başlatır ve belirtilen token'ları subscribe eder.</summary>
    public async Task StartAsync(IReadOnlyList<string> tokenIds, CancellationToken ct = default)
    {
        _subscribedTokenIds = tokenIds;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveTask = ConnectAndReceiveLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    private async Task ConnectAndReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();

            try
            {
                await _webSocket.ConnectAsync(new Uri(_options.WebSocketUrl), ct);
                _logger.LogInformation("Polymarket WebSocket connected");

                await SubscribeAsync(ct);
                await ReceiveLoopAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Polymarket WebSocket disconnected, reconnecting in {Delay}s",
                    ReconnectDelaySeconds);
            }

            if (!ct.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), ct).ConfigureAwait(false);
        }
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        var subscribeMsg = JsonSerializer.Serialize(new
        {
            type       = "subscribe",
            channel    = "market",
            assets_ids = _subscribedTokenIds
        });

        var bytes = Encoding.UTF8.GetBytes(subscribeMsg);
        await _webSocket!.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: ct);

        _logger.LogInformation("Polymarket WebSocket subscribed to {Count} tokens", _subscribedTokenIds.Count);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var pingInterval = TimeSpan.FromSeconds(_options.WsPingIntervalSeconds);
        var lastPing     = DateTime.UtcNow;
        var buffer       = new byte[8192];

        while (!ct.IsCancellationRequested && _webSocket!.State == WebSocketState.Open)
        {
            // Send PING periodically
            if (DateTime.UtcNow - lastPing >= pingInterval)
            {
                await SendPingAsync(ct);
                lastPing = DateTime.UtcNow;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(1));

            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Polymarket WebSocket closed by server");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(message);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Receive timeout — normal, continue loop
            }
        }
    }

    private async Task SendPingAsync(CancellationToken ct)
    {
        try
        {
            var pingMsg = Encoding.UTF8.GetBytes("{\"type\":\"PING\"}");
            await _webSocket!.SendAsync(
                new ArraySegment<byte>(pingMsg),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Polymarket WebSocket ping failed");
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        try
        {
            using var doc  = JsonDocument.Parse(message);
            var root       = doc.RootElement;

            if (!root.TryGetProperty("event_type", out var eventTypeEl))
                return;

            var eventType = eventTypeEl.GetString();

            if (eventType is "price_change" or "last_trade_price")
            {
                var tokenId  = root.TryGetProperty("asset_id", out var tid) ? tid.GetString() ?? string.Empty : string.Empty;
                var priceStr = root.TryGetProperty("price",    out var p)   ? p.GetString()   ?? "0"          : "0";

                if (!string.IsNullOrEmpty(tokenId)
                    && decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price)
                    && OnPriceUpdate is not null)
                {
                    await OnPriceUpdate(tokenId, price);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Polymarket WebSocket message");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        if (_webSocket is not null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
            }
            catch { }
            _webSocket.Dispose();
        }
    }
}
