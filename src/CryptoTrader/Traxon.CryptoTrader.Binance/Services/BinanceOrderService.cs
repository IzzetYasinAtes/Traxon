using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Traxon.CryptoTrader.Binance.Abstractions;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Binance.Services;

/// <summary>
/// Binance REST API uzerinden gercek spot order islemleri yapar.
/// </summary>
public sealed class BinanceOrderService : IBinanceOrderService
{
    private readonly IBinanceRestClient _restClient;
    private readonly ILogger<BinanceOrderService> _logger;
    private readonly ResiliencePipeline _pipeline;

    public BinanceOrderService(
        IBinanceRestClient restClient,
        ILogger<BinanceOrderService> logger)
    {
        _restClient = restClient;
        _logger     = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
                OnRetry          = args =>
                {
                    _logger.LogWarning(
                        "[BinanceOrder] Retry {Attempt} after {Delay}ms: {Exception}",
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
                OnOpened = args =>
                {
                    _logger.LogError(
                        "[BinanceOrder] Circuit breaker OPENED for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("[BinanceOrder] Circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task<Result<long>> PlaceMarketOrderAsync(
        string symbol,
        OrderSide side,
        decimal quantity,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _restClient.SpotApi.Trading.PlaceOrderAsync(
                    symbol:   symbol,
                    side:     side,
                    type:     SpotOrderType.Market,
                    quantity: quantity,
                    ct:       token),
                ct);

            if (!result.Success)
            {
                _logger.LogError(
                    "[BinanceOrder] Market {Side} {Symbol} qty:{Qty} FAILED: {Error}",
                    side, symbol, quantity, result.Error?.Message);
                return Result<long>.Failure(new Error("Binance.OrderFailed", result.Error?.Message ?? "Unknown"));
            }

            _logger.LogInformation(
                "[BinanceOrder] Market {Side} {Symbol} qty:{Qty} orderId:{Id}",
                side, symbol, quantity, result.Data.Id);
            return Result<long>.Success(result.Data.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BinanceOrder] PlaceMarketOrder failed for {Symbol}", symbol);
            return Result<long>.Failure(new Error("Binance.Exception", ex.Message));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<long>> PlaceLimitOrderAsync(
        string symbol,
        OrderSide side,
        decimal quantity,
        decimal price,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _restClient.SpotApi.Trading.PlaceOrderAsync(
                    symbol:      symbol,
                    side:        side,
                    type:        SpotOrderType.Limit,
                    quantity:    quantity,
                    price:       price,
                    timeInForce: TimeInForce.GoodTillCanceled,
                    ct:          token),
                ct);

            if (!result.Success)
            {
                _logger.LogError(
                    "[BinanceOrder] Limit {Side} {Symbol} qty:{Qty} price:{Price} FAILED: {Error}",
                    side, symbol, quantity, price, result.Error?.Message);
                return Result<long>.Failure(new Error("Binance.OrderFailed", result.Error?.Message ?? "Unknown"));
            }

            _logger.LogInformation(
                "[BinanceOrder] Limit {Side} {Symbol} qty:{Qty} price:{Price} orderId:{Id}",
                side, symbol, quantity, price, result.Data.Id);
            return Result<long>.Success(result.Data.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BinanceOrder] PlaceLimitOrder failed for {Symbol}", symbol);
            return Result<long>.Failure(new Error("Binance.Exception", ex.Message));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> CancelOrderAsync(
        string symbol,
        long orderId,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _restClient.SpotApi.Trading.CancelOrderAsync(
                    symbol:  symbol,
                    orderId: orderId,
                    ct:      token),
                ct);

            if (!result.Success)
            {
                _logger.LogError(
                    "[BinanceOrder] CancelOrder {Symbol} orderId:{Id} FAILED: {Error}",
                    symbol, orderId, result.Error?.Message);
                return Result<bool>.Failure(new Error("Binance.CancelFailed", result.Error?.Message ?? "Unknown"));
            }

            _logger.LogInformation("[BinanceOrder] Order {Id} cancelled for {Symbol}", orderId, symbol);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BinanceOrder] CancelOrder failed for {Symbol} orderId:{Id}", symbol, orderId);
            return Result<bool>.Failure(new Error("Binance.Exception", ex.Message));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<decimal>> GetCurrentPriceAsync(
        string symbol,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _restClient.SpotApi.ExchangeData.GetPriceAsync(
                    symbol: symbol,
                    ct:     token),
                ct);

            if (!result.Success)
            {
                _logger.LogError(
                    "[BinanceOrder] GetPrice {Symbol} FAILED: {Error}",
                    symbol, result.Error?.Message);
                return Result<decimal>.Failure(new Error("Binance.PriceFailed", result.Error?.Message ?? "Unknown"));
            }

            return Result<decimal>.Success(result.Data.Price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BinanceOrder] GetCurrentPrice failed for {Symbol}", symbol);
            return Result<decimal>.Failure(new Error("Binance.Exception", ex.Message));
        }
    }
}
