using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IPolymarketClient
{
    Task<Result<PolymarketOrderBook>> GetOrderBookAsync(string tokenId, CancellationToken ct = default);
    Task<Result<decimal>>            GetMidpointAsync(string tokenId, CancellationToken ct = default);
    Task<Result<string>>             PlaceOrderAsync(PolymarketOrderRequest order, CancellationToken ct = default);
    Task<Result<bool>>               CancelOrderAsync(string orderId, CancellationToken ct = default);
    Task<Result<bool>>               SendHeartbeatAsync(CancellationToken ct = default);
    Task<Result<decimal>>            GetBalanceAsync(CancellationToken ct = default);
}
