using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IGammaApiClient
{
    Task<Result<IReadOnlyList<PolymarketMarket>>> GetActiveCryptoMarketsAsync(CancellationToken ct = default);

    /// <summary>
    /// Extended lookback for position resolution — queries Gamma API for markets
    /// going back <paramref name="lookbackMinutes"/> minutes.
    /// </summary>
    Task<Result<IReadOnlyList<PolymarketMarket>>> GetCryptoMarketsWithLookbackAsync(
        int lookbackMinutes, CancellationToken ct = default);
}
