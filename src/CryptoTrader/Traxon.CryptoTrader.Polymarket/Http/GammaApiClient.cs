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

    private static readonly Dictionary<string, string> AssetToSlug = new()
    {
        ["BTC"]  = "btc",
        ["ETH"]  = "eth",
        ["SOL"]  = "sol",
        ["XRP"]  = "xrp",
        ["DOGE"] = "doge",
        ["BNB"]  = "bnb",
        ["HYPE"] = "hype"
    };

    public GammaApiClient(
        HttpClient httpClient,
        IOptions<PolymarketOptions> options,
        ILogger<GammaApiClient> logger)
    {
        _httpClient = httpClient;
        _options    = options.Value;
        _logger     = logger;
    }

    public Task<Result<IReadOnlyList<PolymarketMarket>>> GetActiveCryptoMarketsAsync(
        CancellationToken ct = default)
        => GetCryptoMarketsWithLookbackAsync(20, ct);

    public async Task<Result<IReadOnlyList<PolymarketMarket>>> GetCryptoMarketsWithLookbackAsync(
        int lookbackMinutes, CancellationToken ct = default)
    {
        try
        {
            var now       = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var currentTs = now - (now % 300);

            var windowCount = lookbackMinutes / 5;
            var windowTimestamps = new List<long>(windowCount + 2);
            for (var i = windowCount; i >= 0; i--)
                windowTimestamps.Add(currentTs - i * 300);
            windowTimestamps.Add(currentTs + 300); // next window

            var allMarkets = new List<PolymarketMarket>();
            var semaphore  = new SemaphoreSlim(15); // max 15 concurrent HTTP calls

            var tasks = new List<Task<List<PolymarketMarket>>>();
            foreach (var (asset, slug) in AssetToSlug)
            {
                foreach (var windowTs in windowTimestamps)
                {
                    tasks.Add(FetchSingleWindowAsync(asset, slug, windowTs, semaphore, ct));
                }
            }

            var results = await Task.WhenAll(tasks);
            foreach (var batch in results)
                allMarkets.AddRange(batch);

            var deduplicated = allMarkets
                .GroupBy(m => (m.ConditionId, m.Direction))
                .Select(g => g.First())
                .ToList();

            _logger.LogDebug("Discovered {Count} markets (lookback {Lookback}min, raw {Raw})",
                deduplicated.Count, lookbackMinutes, allMarkets.Count);
            return Result<IReadOnlyList<PolymarketMarket>>.Success(deduplicated.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gamma API unexpected error");
            return Result<IReadOnlyList<PolymarketMarket>>.Failure(
                new Error("Gamma.UnexpectedError", ex.Message));
        }
    }

    private async Task<List<PolymarketMarket>> FetchSingleWindowAsync(
        string asset, string slug, long windowTs, SemaphoreSlim semaphore, CancellationToken ct)
    {
        var results = new List<PolymarketMarket>();
        await semaphore.WaitAsync(ct);
        try
        {
            var eventSlug = $"{slug}-updown-5m-{windowTs}";
            var url       = $"{_options.GammaApiUrl}/events?slug={eventSlug}";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            foreach (var eventEl in doc.RootElement.EnumerateArray())
            {
                if (!eventEl.TryGetProperty("markets", out var marketsArr)
                    || marketsArr.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var marketEl in marketsArr.EnumerateArray())
                    results.AddRange(ParseEventMarket(marketEl, asset));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch window {Slug}-{Ts}", slug, windowTs);
        }
        finally
        {
            semaphore.Release();
        }
        return results;
    }

    /// <summary>
    /// Parses a single market from the events endpoint response.
    /// Each market contains both Up and Down tokens, so we produce two PolymarketMarket instances.
    /// </summary>
    private static List<PolymarketMarket> ParseEventMarket(JsonElement item, string asset)
    {
        var results = new List<PolymarketMarket>(2);

        var conditionId = item.TryGetProperty("conditionId", out var cid) ? cid.GetString() ?? string.Empty : string.Empty;
        var question    = item.TryGetProperty("question",    out var q)   ? q.GetString()   ?? string.Empty : string.Empty;

        if (string.IsNullOrEmpty(conditionId) || string.IsNullOrEmpty(question))
            return results;

        var active = item.TryGetProperty("active", out var activeEl) && activeEl.GetBoolean();
        var closed = item.TryGetProperty("closed", out var closedEl) && closedEl.GetBoolean();

        // No longer filter out closed/inactive markets — CheckPositions needs them to resolve trades.

        // clobTokenIds comes as a JSON-encoded string: "[\"token1\", \"token2\"]"
        var yesTokenId = string.Empty;
        var noTokenId  = string.Empty;
        if (item.TryGetProperty("clobTokenIds", out var tokensEl))
        {
            var tokenIds = ParseJsonStringArray(tokensEl);
            if (tokenIds.Count >= 2)
            {
                yesTokenId = tokenIds[0];
                noTokenId  = tokenIds[1];
            }
        }

        if (string.IsNullOrEmpty(yesTokenId) || string.IsNullOrEmpty(noTokenId))
            return results;

        // outcomes comes as a JSON-encoded string: "[\"Up\", \"Down\"]"
        var outcomes = new List<string>();
        if (item.TryGetProperty("outcomes", out var outcomesEl))
            outcomes = ParseJsonStringArray(outcomesEl);

        if (outcomes.Count < 2)
            return results;

        // endDate: ISO 8601 string -> Unix seconds
        long endDateUtcSeconds = 0;
        if (item.TryGetProperty("endDate", out var endDateEl))
        {
            var endDateStr = endDateEl.GetString();
            if (DateTimeOffset.TryParse(endDateStr, out var endDate))
                endDateUtcSeconds = endDate.ToUnixTimeSeconds();
        }

        // Parse outcomePrices for resolved markets: "[\"0\", \"1\"]" or "[\"1\", \"0\"]"
        decimal? upResolvedPrice  = null;
        decimal? downResolvedPrice = null;
        if (closed && item.TryGetProperty("outcomePrices", out var pricesEl))
        {
            var prices = ParseJsonStringArray(pricesEl);
            if (prices.Count >= 2
                && decimal.TryParse(prices[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p0)
                && decimal.TryParse(prices[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p1))
            {
                upResolvedPrice   = p0; // outcomes[0] = "Up"
                downResolvedPrice = p1; // outcomes[1] = "Down"
            }
        }

        // Create one market per direction (Up uses YesToken, Down uses NoToken)
        results.Add(new PolymarketMarket
        {
            ConditionId       = conditionId,
            Question          = question,
            YesTokenId        = yesTokenId,
            NoTokenId         = noTokenId,
            EndDateUtcSeconds = endDateUtcSeconds,
            Active            = active,
            Closed            = closed,
            UnderlyingAsset   = asset,
            Direction         = "Up",
            ResolvedPrice     = upResolvedPrice
        });

        results.Add(new PolymarketMarket
        {
            ConditionId       = conditionId,
            Question          = question,
            YesTokenId        = yesTokenId,
            NoTokenId         = noTokenId,
            EndDateUtcSeconds = endDateUtcSeconds,
            Active            = active,
            Closed            = closed,
            UnderlyingAsset   = asset,
            Direction         = "Down",
            ResolvedPrice     = downResolvedPrice
        });

        return results;
    }

    /// <summary>
    /// Parses a JsonElement that is either a JSON array or a JSON-encoded string containing an array.
    /// The Gamma events endpoint returns clobTokenIds and outcomes as strings like "[\"Up\", \"Down\"]".
    /// </summary>
    private static List<string> ParseJsonStringArray(JsonElement element)
    {
        var result = new List<string>();

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                result.Add(item.GetString() ?? string.Empty);
            return result;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            if (string.IsNullOrEmpty(raw))
                return result;

            try
            {
                using var innerDoc = JsonDocument.Parse(raw);
                if (innerDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in innerDoc.RootElement.EnumerateArray())
                        result.Add(item.GetString() ?? string.Empty);
                }
            }
            catch
            {
                // Malformed JSON string — ignore
            }
        }

        return result;
    }
}
