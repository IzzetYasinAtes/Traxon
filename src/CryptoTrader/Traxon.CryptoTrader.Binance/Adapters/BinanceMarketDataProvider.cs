using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Binance.Mappers;
using Traxon.CryptoTrader.Binance.Options;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Binance.Adapters;

public sealed class BinanceMarketDataProvider : IMarketDataProvider, IAsyncDisposable
{
    private readonly IBinanceRestClient _restClient;
    private readonly IBinanceSocketClient _socketClient;
    private readonly ILogger<BinanceMarketDataProvider> _logger;
    private readonly BinanceOptions _options;
    private readonly ResiliencePipeline _restPipeline;

    private readonly List<UpdateSubscription> _subscriptions = [];
    private bool _isConnected;

    public bool IsConnected => _isConnected;

    public BinanceMarketDataProvider(
        IBinanceRestClient restClient,
        IBinanceSocketClient socketClient,
        ILogger<BinanceMarketDataProvider> logger,
        IOptions<BinanceOptions> options)
    {
        _restClient = restClient;
        _socketClient = socketClient;
        _logger = logger;
        _options = options.Value;

        _restPipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts  = 3,
                Delay             = TimeSpan.FromSeconds(2),
                BackoffType       = DelayBackoffType.Exponential,
                UseJitter         = true,
                OnRetry           = args =>
                {
                    _logger.LogWarning(
                        "Binance REST retry {Attempt} after {Delay}ms: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "no exception");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio      = 0.5,
                SamplingDuration  = TimeSpan.FromSeconds(30),
                MinimumThroughput = 3,
                BreakDuration     = TimeSpan.FromSeconds(60),
                OnOpened          = args =>
                {
                    _logger.LogError(
                        "Binance REST circuit breaker OPENED for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Binance REST circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<Result<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        Asset asset,
        TimeFrame timeFrame,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var interval = ToKlineInterval(timeFrame);

        try
        {
            var result = await _restPipeline.ExecuteAsync(
                async ct => await _restClient.SpotApi.ExchangeData.GetKlinesAsync(
                    symbol: asset.Symbol,
                    interval: interval,
                    limit: limit,
                    ct: ct),
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Binance REST klines failed for {Symbol}/{Interval}: {Error}",
                    asset.Symbol, timeFrame.Value, result.Error?.Message);
                return Result<IReadOnlyList<Candle>>.Failure(
                    new Error("Binance.RestFailed", result.Error?.Message ?? "Unknown error"));
            }

            IReadOnlyList<Candle> candles = result.Data
                .Select(k => BinanceMapper.ToCandle(k, asset, timeFrame))
                .OrderBy(c => c.OpenTime)
                .ToList()
                .AsReadOnly();

            return Result<IReadOnlyList<Candle>>.Success(candles);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Binance REST circuit open — skipping {Symbol}/{Interval}", asset.Symbol, timeFrame.Value);
            return Result<IReadOnlyList<Candle>>.Failure(
                new Error("Binance.CircuitOpen", "Circuit breaker is open, request rejected."));
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Binance REST timeout for {Symbol}/{Interval}", asset.Symbol, timeFrame.Value);
            return Result<IReadOnlyList<Candle>>.Failure(
                new Error("Binance.Timeout", "REST request timed out."));
        }
    }

    public async Task StartStreamAsync(
        IReadOnlyList<Asset> assets,
        IReadOnlyList<TimeFrame> timeFrames,
        Func<Candle, Task> onCandleClosed,
        CancellationToken cancellationToken = default)
    {
        var streams = new List<(string symbol, KlineInterval interval, Asset asset, TimeFrame tf)>();

        foreach (var asset in assets)
        foreach (var tf in timeFrames)
            streams.Add((asset.Symbol.ToLowerInvariant(), ToKlineInterval(tf), asset, tf));

        _logger.LogInformation("Starting Binance WebSocket for {StreamCount} streams", streams.Count);

        foreach (var (symbol, interval, asset, tf) in streams)
        {
            var sub = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbol: symbol,
                interval: interval,
                onMessage: async data =>
                {
                    try
                    {
                        var candle = BinanceMapper.ToCandle(data.Data, asset, tf);

                        if (candle.IsClosed)
                        {
                            _logger.LogDebug("Candle closed: {Symbol} {Interval} @ {CloseTime}",
                                asset.Symbol, tf.Value, candle.CloseTime);
                            await onCandleClosed(candle);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing kline update for {Symbol}/{Interval}",
                            asset.Symbol, tf.Value);
                    }
                },
                ct: cancellationToken);

            if (sub.Success)
            {
                _subscriptions.Add(sub.Data);
                _logger.LogInformation("Subscribed: {Symbol}@kline_{Interval}", symbol, interval);
            }
            else
            {
                _logger.LogError("Failed to subscribe: {Symbol}@kline_{Interval} — {Error}",
                    symbol, interval, sub.Error?.Message);
            }
        }

        _isConnected = _subscriptions.Count > 0;
    }

    public async Task StopStreamAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sub in _subscriptions)
            await sub.CloseAsync();

        _subscriptions.Clear();
        _isConnected = false;
        _logger.LogInformation("Binance WebSocket streams stopped");
    }

    private static KlineInterval ToKlineInterval(TimeFrame tf) => tf.Value switch
    {
        "5m"  => KlineInterval.FiveMinutes,
        "15m" => KlineInterval.FifteenMinutes,
        _     => throw new ArgumentOutOfRangeException(nameof(tf), $"Unsupported timeframe: {tf.Value}")
    };

    public async ValueTask DisposeAsync()
    {
        await StopStreamAsync();
        _restClient.Dispose();
        _socketClient.Dispose();
    }
}
