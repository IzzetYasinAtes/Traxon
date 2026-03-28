using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface ITradeLogger
{
    Task LogSignalAsync(Signal signal, CancellationToken ct = default);
    Task LogTradeOpenedAsync(Trade trade, CancellationToken ct = default);
    Task LogTradeClosedAsync(Trade trade, CancellationToken ct = default);
    Task LogPortfolioSnapshotAsync(Portfolio portfolio, CancellationToken ct = default);

    /// <summary>
    /// Worker restart sonrasi in-memory state'i restore etmek icin DB'deki acik trade'leri doner.
    /// </summary>
    Task<IReadOnlyList<Trade>> GetOpenTradesAsync(string engineName, CancellationToken ct = default);
}
