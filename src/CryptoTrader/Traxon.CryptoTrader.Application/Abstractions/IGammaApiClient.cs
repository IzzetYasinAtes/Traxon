using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IGammaApiClient
{
    Task<Result<IReadOnlyList<PolymarketMarket>>> GetActiveCryptoMarketsAsync(CancellationToken ct = default);
}
