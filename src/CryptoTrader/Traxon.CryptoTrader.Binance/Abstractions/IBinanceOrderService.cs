using Binance.Net.Enums;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Binance.Abstractions;

public interface IBinanceOrderService
{
    /// <summary>Spot market order. Returns Binance orderId.</summary>
    Task<Result<long>> PlaceMarketOrderAsync(
        string symbol,
        OrderSide side,
        decimal quantity,
        CancellationToken ct = default);

    /// <summary>Spot limit order. Returns Binance orderId.</summary>
    Task<Result<long>> PlaceLimitOrderAsync(
        string symbol,
        OrderSide side,
        decimal quantity,
        decimal price,
        CancellationToken ct = default);

    /// <summary>Cancel an open order by orderId.</summary>
    Task<Result<bool>> CancelOrderAsync(
        string symbol,
        long orderId,
        CancellationToken ct = default);

    /// <summary>Get current spot price for the symbol.</summary>
    Task<Result<decimal>> GetCurrentPriceAsync(
        string symbol,
        CancellationToken ct = default);
}
