using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IMarketDiscoveryService
{
    Task<Result<IReadOnlyList<PolymarketMarket>>> DiscoverMarketsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns ALL markets including closed/inactive ones (for resolving existing positions).
    /// Only filters for non-empty direction and underlyingAsset.
    /// </summary>
    Task<Result<IReadOnlyList<PolymarketMarket>>> DiscoverAllMarketsAsync(CancellationToken ct = default);
}
