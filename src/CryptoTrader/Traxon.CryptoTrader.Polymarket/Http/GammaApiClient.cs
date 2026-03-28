using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.Http;

public sealed class GammaApiClient : IGammaApiClient
{
    private readonly HttpClient              _httpClient;
    private readonly PolymarketOptions       _options;
    private readonly ILogger<GammaApiClient> _logger;

    private static readonly string[] SupportedAssets = ["BTC", "ETH", "SOL", "XRP", "DOGE", "BNB", "HYPE"];
    private static readonly string[] UpKeywords      = ["Up", "up", "UP"];
    private static readonly string[] DownKeywords    = ["Down", "down", "DOWN"];

    public GammaApiClient(
        HttpClient httpClient,
        IOptions<PolymarketOptions> options,
        ILogger<GammaApiClient> logger)
    {
        _httpClient = httpClient;
        _options    = options.Value;
        _logger     = logger;
    }

    public async Task<Result<IReadOnlyList<PolymarketMarket>>> GetActiveCryptoMarketsAsync(
        CancellationToken ct = default)
    {
        try
        {
            var url      = $"{_options.GammaApiUrl}/markets?closed=false&limit=100&tag_slug=crypto";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gamma API failed: {Status}", response.StatusCode);
                return Result<IReadOnlyList<PolymarketMarket>>.Failure(
                    new Error("Gamma.HttpError", $"HTTP {(int)response.StatusCode}"));
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var markets = new List<PolymarketMarket>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var market = ParseMarket(item);
                if (market is not null)
                    markets.Add(market);
            }

            return Result<IReadOnlyList<PolymarketMarket>>.Success(markets.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gamma API unexpected error");
            return Result<IReadOnlyList<PolymarketMarket>>.Failure(
                new Error("Gamma.UnexpectedError", ex.Message));
        }
    }

    private static PolymarketMarket? ParseMarket(JsonElement item)
    {
        var conditionId = item.TryGetProperty("conditionId", out var cid) ? cid.GetString() ?? string.Empty : string.Empty;
        var question    = item.TryGetProperty("question",    out var q)   ? q.GetString()   ?? string.Empty : string.Empty;

        if (string.IsNullOrEmpty(conditionId) || string.IsNullOrEmpty(question))
            return null;

        // clobTokenIds: array with [YesTokenId, NoTokenId]
        var yesTokenId = string.Empty;
        var noTokenId  = string.Empty;
        if (item.TryGetProperty("clobTokenIds", out var tokens) && tokens.ValueKind == JsonValueKind.Array)
        {
            var tokenArr = tokens.EnumerateArray().ToList();
            if (tokenArr.Count >= 2)
            {
                yesTokenId = tokenArr[0].GetString() ?? string.Empty;
                noTokenId  = tokenArr[1].GetString() ?? string.Empty;
            }
        }

        // endDate: ISO 8601 string → Unix seconds
        long endDateUtcSeconds = 0;
        if (item.TryGetProperty("endDate", out var endDateEl))
        {
            var endDateStr = endDateEl.GetString();
            if (DateTimeOffset.TryParse(endDateStr, out var endDate))
                endDateUtcSeconds = endDate.ToUnixTimeSeconds();
        }

        var active = item.TryGetProperty("active", out var activeEl) && activeEl.GetBoolean();
        var closed = item.TryGetProperty("closed", out var closedEl) && closedEl.GetBoolean();

        // Determine underlying asset
        var questionUpper   = question.ToUpperInvariant();
        var underlyingAsset = SupportedAssets.FirstOrDefault(a => questionUpper.Contains(a)) ?? string.Empty;

        if (string.IsNullOrEmpty(underlyingAsset))
            return null;

        // Determine direction
        var direction = string.Empty;
        if (UpKeywords.Any(k => question.Contains(k, StringComparison.Ordinal)))
            direction = "Up";
        else if (DownKeywords.Any(k => question.Contains(k, StringComparison.Ordinal)))
            direction = "Down";

        if (string.IsNullOrEmpty(direction))
            return null;

        return new PolymarketMarket
        {
            ConditionId       = conditionId,
            Question          = question,
            YesTokenId        = yesTokenId,
            NoTokenId         = noTokenId,
            EndDateUtcSeconds = endDateUtcSeconds,
            Active            = active,
            Closed            = closed,
            UnderlyingAsset   = underlyingAsset,
            Direction         = direction
        };
    }
}
