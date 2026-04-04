using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Polymarket.Options;

namespace Traxon.CryptoTrader.Polymarket.Services;

public sealed class MarketDiscoveryService : IMarketDiscoveryService
{
    private readonly IGammaApiClient              _gammaClient;
    private readonly PolymarketOptions            _options;
    private readonly ILogger<MarketDiscoveryService> _logger;

    public MarketDiscoveryService(
        IGammaApiClient gammaClient,
        IOptions<PolymarketOptions> options,
        ILogger<MarketDiscoveryService> logger)
    {
        _gammaClient = gammaClient;
        _options     = options.Value;
        _logger      = logger;
    }

    public async Task<Result<IReadOnlyList<PolymarketMarket>>> DiscoverMarketsAsync(
        CancellationToken ct = default)
    {
        var result = await _gammaClient.GetActiveCryptoMarketsAsync(ct);

        if (result.IsFailure)
            return result;

        var minTimeLeft = TimeSpan.FromMinutes(_options.MarketMinutesMinRemaining);

        var filtered = result.Value!
            .Where(m => m.Active
                     && !m.Closed
                     && m.TimeLeft >= minTimeLeft
                     && !string.IsNullOrEmpty(m.Direction)
                     && !string.IsNullOrEmpty(m.UnderlyingAsset))
            .ToList()
            .AsReadOnly();

        _logger.LogInformation(
            "MarketDiscovery: {Total} markets found, {Filtered} passed filters",
            result.Value!.Count, filtered.Count);

        return Result<IReadOnlyList<PolymarketMarket>>.Success(filtered);
    }

    /// <summary>
    /// Tüm marketleri döndürür (kapanmış dahil). CheckPositionsAsync için kullanılır.
    /// </summary>
    public async Task<Result<IReadOnlyList<PolymarketMarket>>> DiscoverAllMarketsAsync(
        CancellationToken ct = default)
    {
        var result = await _gammaClient.GetActiveCryptoMarketsAsync(ct);
        if (result.IsFailure)
            return result;

        var all = result.Value!
            .Where(m => !string.IsNullOrEmpty(m.Direction)
                     && !string.IsNullOrEmpty(m.UnderlyingAsset))
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<PolymarketMarket>>.Success(all);
    }
}