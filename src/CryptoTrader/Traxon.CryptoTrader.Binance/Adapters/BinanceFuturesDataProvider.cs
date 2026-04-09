using System.Collections.Concurrent;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Binance.Adapters;

public sealed class BinanceFuturesDataProvider : IFuturesDataProvider
{
    private readonly IBinanceSocketClient _socketClient;
    private readonly IBinanceRestClient _restClient;
    private readonly ILogger<BinanceFuturesDataProvider> _logger;

    private readonly ConcurrentDictionary<string, decimal> _fundingRates = new();
    private readonly ConcurrentDictionary<string, decimal> _openInterest = new();
    private readonly ConcurrentDictionary<string, decimal> _openInterestPrev = new();
    private readonly ConcurrentDictionary<string, decimal> _orderBookImbalance = new();
    private readonly ConcurrentDictionary<string, List<decimal>> _obiHistory = new();

    private readonly List<UpdateSubscription> _subscriptions = [];
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    private const int ObiHistorySize = 12;
    private static readonly decimal[] ObiWeights = [1.0m, 0.5m, 0.25m, 0.125m, 0.0625m];

    public BinanceFuturesDataProvider(
        IBinanceSocketClient socketClient,
        IBinanceRestClient restClient,
        ILogger<BinanceFuturesDataProvider> logger)
    {
        _socketClient = socketClient;
        _restClient = restClient;
        _logger = logger;
    }

    public decimal GetFundingRate(string symbol)
        => _fundingRates.TryGetValue(symbol, out var rate) ? rate : 0m;

    public decimal GetOpenInterest(string symbol)
        => _openInterest.TryGetValue(symbol, out var oi) ? oi : 0m;

    public decimal GetOrderBookImbalance(string symbol)
    {
        if (!_obiHistory.TryGetValue(symbol, out var history) || history.Count == 0)
            return 0m;

        decimal avg;
        decimal persistence;
        lock (history)
        {
            if (history.Count == 0) return 0m;
            avg = history.Average();
            persistence = (decimal)history.Count(v => Math.Sign(v) == Math.Sign(avg)) / history.Count;
        }
        return avg * persistence;
    }

    public async Task StartAsync(IReadOnlyList<Asset> assets, CancellationToken ct)
    {
        _logger.LogInformation("FuturesDataProvider starting for {Count} assets...", assets.Count);

        // 1) Mark Price stream — funding rates
        try
        {
            var markPriceResult = await _socketClient.UsdFuturesApi.ExchangeData
                .SubscribeToAllMarkPriceUpdatesAsync(1000, data =>
                {
                    foreach (var item in data.Data)
                    {
                        var sym = item.Symbol;
                        if (item.FundingRate.HasValue)
                            _fundingRates[sym] = item.FundingRate.Value;
                    }
                }, ct);

            if (markPriceResult.Success)
            {
                _subscriptions.Add(markPriceResult.Data);
                _logger.LogInformation("Subscribed to mark price updates (funding rates)");
            }
            else
            {
                _logger.LogWarning("Failed to subscribe to mark price: {Error}", markPriceResult.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mark price subscription failed");
        }

        // 2) Order Book depth streams per asset
        foreach (var asset in assets)
        {
            try
            {
                var obResult = await _socketClient.UsdFuturesApi.ExchangeData
                    .SubscribeToPartialOrderBookUpdatesAsync(asset.Symbol, 20, 500, data =>
                    {
                        var bids = data.Data.Bids.Take(5).ToList();
                        var asks = data.Data.Asks.Take(5).ToList();

                        var weightedBid = 0m;
                        var weightedAsk = 0m;
                        for (int i = 0; i < Math.Min(5, Math.Min(bids.Count, asks.Count)); i++)
                        {
                            weightedBid += bids[i].Quantity * ObiWeights[i];
                            weightedAsk += asks[i].Quantity * ObiWeights[i];
                        }

                        var total = weightedBid + weightedAsk;
                        var obi = total > 0 ? (weightedBid - weightedAsk) / total : 0m;

                        _orderBookImbalance[asset.Symbol] = obi;

                        // Rolling history
                        var history = _obiHistory.GetOrAdd(asset.Symbol, _ => new List<decimal>());
                        lock (history)
                        {
                            history.Add(obi);
                            while (history.Count > ObiHistorySize)
                                history.RemoveAt(0);
                        }
                    }, ct);

                if (obResult.Success)
                {
                    _subscriptions.Add(obResult.Data);
                }
                else
                {
                    _logger.LogWarning("Failed to subscribe to order book for {Symbol}: {Error}",
                        asset.Symbol, obResult.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Order book subscription failed for {Symbol}", asset.Symbol);
            }
        }

        _logger.LogInformation("Subscribed to {Count} order book streams", _subscriptions.Count - 1);

        // 3) Open Interest polling
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollTask = PollOpenInterestAsync(assets, _pollCts.Token);

        _logger.LogInformation("FuturesDataProvider started successfully");
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("FuturesDataProvider stopping...");

        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync();
            if (_pollTask is not null)
            {
                try { await _pollTask; }
                catch (OperationCanceledException) { }
            }
            _pollCts.Dispose();
        }

        foreach (var sub in _subscriptions)
        {
            try { await sub.CloseAsync(); }
            catch { /* best effort */ }
        }
        _subscriptions.Clear();

        _logger.LogInformation("FuturesDataProvider stopped");
    }

    private async Task PollOpenInterestAsync(IReadOnlyList<Asset> assets, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var asset in assets)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var result = await _restClient.UsdFuturesApi.ExchangeData
                        .GetOpenInterestAsync(asset.Symbol, ct);

                    if (result.Success)
                    {
                        var currentOi = result.Data.OpenInterest;
                        if (_openInterest.TryGetValue(asset.Symbol, out var prevOi))
                            _openInterestPrev[asset.Symbol] = prevOi;

                        _openInterest[asset.Symbol] = currentOi;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "OI poll failed for {Symbol}", asset.Symbol);
                }
            }

            try { await Task.Delay(TimeSpan.FromSeconds(60), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
