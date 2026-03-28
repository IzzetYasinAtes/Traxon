using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IMarketDiscoveryService
{
    Task<Result<IReadOnlyList<PolymarketMarket>>> DiscoverMarketsAsync(CancellationToken ct = default);
}
