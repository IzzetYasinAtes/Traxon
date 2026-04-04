using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Polymarket.Http;

public sealed class PolymarketSigningClient : IPolymarketSigningClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PolymarketSigningClient> _logger;
    private readonly ResiliencePipeline _pipeline;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PolymarketSigningClient(
        HttpClient httpClient,
        ILogger<PolymarketSigningClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "SigningClient retry {Attempt} after {Delay}ms: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "no exception");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<Result<string>> CreateAndPostOrderAsync(
        string tokenId, decimal price, decimal size, string side,
        string orderType = "GTC", CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                token_id = tokenId,
                price = price.ToString(CultureInfo.InvariantCulture),
                size = size.ToString(CultureInfo.InvariantCulture),
                side,
                order_type = orderType
            }, JsonOptions);

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _pipeline.ExecuteAsync(
                async token => await _httpClient.PostAsync("/create-and-post", content, token),
                ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

            if (!success)
            {
                var error = root.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Unknown error"
                    : "Unknown error";
                _logger.LogError("SigningClient CreateAndPostOrder failed: {Error}", error);
                return Result<string>.Failure(new Error("Signer.OrderFailed", error));
            }

            // Extract orderId from response
            var responseObj = root.TryGetProperty("response", out var respProp)
                ? respProp.GetRawText()
                : "";

            // Try to get orderID from nested response
            var orderId = "";
            if (root.TryGetProperty("response", out var respElement))
            {
                if (respElement.ValueKind == JsonValueKind.Object &&
                    respElement.TryGetProperty("orderID", out var orderIdProp))
                {
                    orderId = orderIdProp.GetString() ?? "";
                }
                else if (respElement.ValueKind == JsonValueKind.String)
                {
                    orderId = respElement.GetString() ?? "";
                }
                else
                {
                    orderId = respElement.GetRawText();
                }
            }

            _logger.LogInformation("SigningClient order posted: {OrderId}", orderId);
            return Result<string>.Success(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SigningClient CreateAndPostOrder unexpected error");
            return Result<string>.Failure(new Error("Signer.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<string>> CreateAndPostMarketOrderAsync(
        string tokenId, decimal amountUsdc, string side,
        string orderType = "FAK", CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                token_id = tokenId,
                amount = amountUsdc.ToString(CultureInfo.InvariantCulture),
                side,
                order_type = orderType
            }, JsonOptions);

            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _pipeline.ExecuteAsync(
                async token => await _httpClient.PostAsync("/create-and-post", content, token),
                ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

            if (!success)
            {
                var error = root.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Unknown error"
                    : "Unknown error";
                _logger.LogError("SigningClient MarketOrder failed: {Error}", error);
                return Result<string>.Failure(new Error("Signer.MarketOrderFailed", error));
            }

            var orderId = "";
            if (root.TryGetProperty("response", out var respElement))
            {
                if (respElement.ValueKind == JsonValueKind.Object &&
                    respElement.TryGetProperty("orderID", out var orderIdProp))
                    orderId = orderIdProp.GetString() ?? "";
                else if (respElement.ValueKind == JsonValueKind.String)
                    orderId = respElement.GetString() ?? "";
                else
                    orderId = respElement.GetRawText();
            }

            _logger.LogInformation("SigningClient market order posted: {OrderId}", orderId);
            return Result<string>.Success(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SigningClient MarketOrder unexpected error");
            return Result<string>.Failure(new Error("Signer.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<bool>> CancelOrderAsync(string orderId, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { order_id = orderId }, JsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _pipeline.ExecuteAsync(
                async token => await _httpClient.PostAsync("/cancel", content, token),
                ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

            if (!success)
            {
                var error = root.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Unknown error"
                    : "Unknown error";
                _logger.LogError("SigningClient CancelOrder failed: {Error}", error);
                return Result<bool>.Failure(new Error("Signer.CancelFailed", error));
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SigningClient CancelOrder unexpected error");
            return Result<bool>.Failure(new Error("Signer.UnexpectedError", ex.Message));
        }
    }

    public async Task<Result<decimal>> GetBalanceAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _pipeline.ExecuteAsync(
                async token => await _httpClient.GetAsync("/balance", token),
                ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

            if (!success)
            {
                var error = root.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Unknown error"
                    : "Unknown error";
                return Result<decimal>.Failure(new Error("Signer.BalanceFailed", error));
            }

            var balance = root.TryGetProperty("balance_usdc", out var balProp)
                ? balProp.GetDecimal()
                : 0m;

            return Result<decimal>.Success(balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SigningClient GetBalance unexpected error");
            return Result<decimal>.Failure(new Error("Signer.UnexpectedError", ex.Message));
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", ct);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("status", out var statusProp)
                   && statusProp.GetString() == "healthy";
        }
        catch
        {
            return false;
        }
    }
}
