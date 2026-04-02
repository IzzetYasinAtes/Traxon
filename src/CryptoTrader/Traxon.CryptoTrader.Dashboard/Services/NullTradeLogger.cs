using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>Dashboard icin no-op trade logger. Persistans Worker'a birakilir.</summary>
internal sealed class NullTradeLogger : ITradeLogger
{
    public Task LogSignalAsync(Signal signal, CancellationToken ct = default) => Task.CompletedTask;
    public Task LogTradeOpenedAsync(Trade trade, CancellationToken ct = default) => Task.CompletedTask;
    public Task LogTradeClosedAsync(Trade trade, CancellationToken ct = default) => Task.CompletedTask;
    public Task LogPortfolioSnapshotAsync(Portfolio portfolio, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Trade>> GetOpenTradesAsync(string engineName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Trade>>(Array.Empty<Trade>());
    public Task<PortfolioSnapshotDto?> GetLatestSnapshotAsync(string engineName, CancellationToken ct = default)
        => Task.FromResult<PortfolioSnapshotDto?>(null);
    public Task LogSignalWithResultsAsync(Signal signal, IReadOnlyList<(string engineName, bool accepted, string? rejectionCode, Guid? tradeId)> engineResults, CancellationToken ct = default)
        => Task.CompletedTask;
}
