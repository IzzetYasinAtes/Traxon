using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.Http;

public sealed class PolymarketClient : IPolymarketClient
{
    private readonly HttpClient              _httpClient;
    private readonly PolymarketOptions       _options;
    private readonly ILogger<PolymarketClient> _logger;
    private readonly ResiliencePipeline      _restPipeline;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PolymarketClient(
        HttpClient httpClient,
        IOptions<PolymarketOptions> options,
        ILogger<PolymarketClient> logger)
    {
        _httpClient = httpClient;
        _options    = options.Value;
        _logger     = logger;

        _restPipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(2),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
                OnRetry          = args =>
                {
                    _logger.LogWarning(
                        "Polymarket REST retry {Attempt} after {Delay}ms: {Exception}",
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
                        "Polymarket REST circuit breaker OPENED for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Polymarket REST circuit breaker closed");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<Result<PolymarketOrderBook>> GetOrderBookAsync(
        string tokenId, CancellationToken ct = default)
    {
        try
        {
            var response = await _restPipeline.ExecuteAsync(
                async token => await _httpClient.GetAsync($"/book?token_id={tokenId}", token),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Polymarket GetOrderBook failed: {Status}", response.StatusCode);
                return Result<PolymarketOrderBook>.Failure(
                    new Error("Polymarket.HttpError", $"HTTP {(int)response.StatusCode}"));
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var bids = ParseLevels(root, "bids");
            var asks = ParseLevels(root, "asks");

            var orderBook = new PolymarketOrderBook
            {
                TokenId = tokenId,
                Bids    = bids,
                Asks    = asks
            };

            return Result<PolymarketOrderBook>.Success(orderBook);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Polymarket circuit open — GetOrderBook rejected");
            return Result<PolymarketOrderBook>.Failure(
                new Error("Polymarket.CircuitOpen", "Circuit breaker is open."));
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Polymarket timeout — GetOrderBook");
            return Result<PolymarketOrderBook>.Failure(
                new Error("Polymarket.Timeout", "Request timed out."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Polymarket GetOrderBook unexpected error");
            return Result<PolymarketOrderBook>.Failure(
                new Error("Polymarket.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<decimal>> GetMidpointAsync(
        string tokenId, CancellationToken ct = default)
    {
        try
        {
            var response = await _restPipeline.ExecuteAsync(
                async token => await _httpClient.GetAsync($"/midpoint?token_id={tokenId}", token),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Polymarket GetMidpoint failed: {Status}", response.StatusCode);
                return Result<decimal>.Failure(
                    new Error("Polymarket.HttpError", $"HTTP {(int)response.StatusCode}"));
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var mid = doc.RootElement.GetProperty("mid").GetString();

            if (!decimal.TryParse(mid, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var midpoint))
            {
                return Result<decimal>.Failure(
                    new Error("Polymarket.ParseError", "Could not parse midpoint value."));
            }

            return Result<decimal>.Success(midpoint);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Polymarket circuit open — GetMidpoint rejected");
            return Result<decimal>.Failure(
                new Error("Polymarket.CircuitOpen", "Circuit breaker is open."));
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Polymarket timeout — GetMidpoint");
            return Result<decimal>.Failure(
                new Error("Polymarket.Timeout", "Request timed out."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Polymarket GetMidpoint unexpected error");
            return Result<decimal>.Failure(
                new Error("Polymarket.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<string>> PlaceOrderAsync(
        PolymarketOrderRequest order, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Result<string>.Failure(
                new Error("Polymarket.Disabled",
                    "Polymarket engine is disabled. Set Polymarket:Enabled=true in configuration."));

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                token_id   = order.TokenId,
                price      = order.Price,
                size       = order.Size,
                side       = order.Side,
                type       = order.OrderType,
                signature  = order.Signature
            }, JsonOptions);

            var content  = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _restPipeline.ExecuteAsync(
                async token => await _httpClient.PostAsync("/order", content, token),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Polymarket PlaceOrder failed: {Status}", response.StatusCode);
                return Result<string>.Failure(
                    new Error("Polymarket.HttpError", $"HTTP {(int)response.StatusCode}"));
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var orderId = doc.RootElement.GetProperty("orderID").GetString() ?? string.Empty;

            return Result<string>.Success(orderId);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Polymarket circuit open — PlaceOrder rejected");
            return Result<string>.Failure(
                new Error("Polymarket.CircuitOpen", "Circuit breaker is open."));
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Polymarket timeout — PlaceOrder");
            return Result<string>.Failure(
                new Error("Polymarket.Timeout", "Request timed out."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Polymarket PlaceOrder unexpected error");
            return Result<string>.Failure(
                new Error("Polymarket.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<bool>> CancelOrderAsync(
        string orderId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Result<bool>.Failure(
                new Error("Polymarket.Disabled",
                    "Polymarket engine is disabled. Set Polymarket:Enabled=true in configuration."));

        try
        {
            var response = await _restPipeline.ExecuteAsync(
                async token => await _httpClient.DeleteAsync($"/order/{orderId}", token),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Polymarket CancelOrder failed: {Status}", response.StatusCode);
                return Result<bool>.Failure(
                    new Error("Polymarket.HttpError", $"HTTP {(int)response.StatusCode}"));
            }

            return Result<bool>.Success(true);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Polymarket circuit open — CancelOrder rejected");
            return Result<bool>.Failure(
                new Error("Polymarket.CircuitOpen", "Circuit breaker is open."));
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogError(ex, "Polymarket timeout — CancelOrder");
            return Result<bool>.Failure(
                new Error("Polymarket.Timeout", "Request timed out."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Polymarket CancelOrder unexpected error");
            return Result<bool>.Failure(
                new Error("Polymarket.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<bool>> SendHeartbeatAsync(CancellationToken ct = default)
    {
        try
        {
            var content  = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _restPipeline.ExecuteAsync(
                async token => await _httpClient.PostAsync("/heartbeats", content, token),
                ct);

            return response.IsSuccessStatusCode
                ? Result<bool>.Success(true)
                : Result<bool>.Failure(
                    new Error("Polymarket.HeartbeatFailed", $"HTTP {(int)response.StatusCode}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Polymarket heartbeat failed");
            return Result<bool>.Failure(
                new Error("Polymarket.HeartbeatFailed", ex.Message));
        }
    }

    private static IReadOnlyList<PolymarketLevel> ParseLevels(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var arr))
            return [];

        var levels = new List<PolymarketLevel>();
        foreach (var item in arr.EnumerateArray())
        {
            var priceStr = item.GetProperty("price").GetString() ?? "0";
            var sizeStr  = item.GetProperty("size").GetString()  ?? "0";

            if (decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var price)
                && decimal.TryParse(sizeStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var size))
            {
                levels.Add(new PolymarketLevel(price, size));
            }
        }

        return levels.AsReadOnly();
    }
}
