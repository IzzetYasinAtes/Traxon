using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IPolymarketSigningClient
{
    Task<Result<string>> CreateAndPostOrderAsync(
        string tokenId, decimal price, decimal size, string side,
        string orderType = "GTC", CancellationToken ct = default);

    Task<Result<bool>> CancelOrderAsync(string orderId, CancellationToken ct = default);

    Task<Result<decimal>> GetBalanceAsync(CancellationToken ct = default);

    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
