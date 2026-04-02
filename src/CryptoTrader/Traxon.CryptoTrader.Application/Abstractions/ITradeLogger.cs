using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Abstractions;

/// <summary>
/// Son portfolio snapshot verisini tasir. Infrastructure → Application katmani arasinda kopru.
/// </summary>
public sealed record PortfolioSnapshotDto(
    decimal Balance,
    decimal TotalPnL,
    int     TradeCount,
    decimal? WinRate);

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

    /// <summary>
    /// Belirtilen engine icin son portfolio snapshot'i doner. Yoksa null.
    /// </summary>
    Task<PortfolioSnapshotDto?> GetLatestSnapshotAsync(string engineName, CancellationToken ct = default);

    /// <summary>
    /// Sinyal ve tum engine sonuclarini (accept/reject) birlikte DB'ye yazar.
    /// </summary>
    Task LogSignalWithResultsAsync(
        Signal signal,
        IReadOnlyList<(string engineName, bool accepted, string? rejectionCode, Guid? tradeId)> engineResults,
        CancellationToken ct = default);
}
