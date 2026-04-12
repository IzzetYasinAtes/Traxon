using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IFuturesDataProvider
{
    decimal GetFundingRate(string symbol);
    decimal GetOpenInterest(string symbol);
    decimal GetOrderBookImbalance(string symbol);
    decimal GetOrderBookMomentum(string symbol);
    decimal GetOpenInterestChange(string symbol);
    decimal GetCKOrderFlowImbalance(string symbol);
    Task StartAsync(IReadOnlyList<Asset> assets, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
