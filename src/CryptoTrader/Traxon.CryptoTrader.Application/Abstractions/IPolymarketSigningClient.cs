using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IPolymarketSigningClient
{
    Task<Result<string>> CreateAndPostOrderAsync(
        string tokenId, decimal price, decimal size, string side,
        string orderType = "GTC", CancellationToken ct = default);

    /// <summary>FAK/FOK market order — amount in USDC, fills immediately</summary>
    Task<Result<string>> CreateAndPostMarketOrderAsync(
        string tokenId, decimal amountUsdc, string side,
        string orderType = "FAK", CancellationToken ct = default);

    Task<Result<bool>> CancelOrderAsync(string orderId, CancellationToken ct = default);

    Task<Result<decimal>> GetBalanceAsync(CancellationToken ct = default);

    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
