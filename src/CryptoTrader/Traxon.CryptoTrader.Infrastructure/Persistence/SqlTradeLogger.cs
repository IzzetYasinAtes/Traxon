using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Infrastructure.Persistence.Models;
using TradeStatus = Traxon.CryptoTrader.Domain.Trading.TradeStatus;

namespace Traxon.CryptoTrader.Infrastructure.Persistence;

public sealed class SqlTradeLogger : ITradeLogger
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<SqlTradeLogger>         _logger;

    public SqlTradeLogger(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<SqlTradeLogger> logger)
    {
        _dbFactory = dbFactory;
        _logger    = logger;
    }

    public async Task LogSignalAsync(Signal signal, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Signal logged: {Symbol}/{Interval} {Direction} FV:{FV:F3} Edge:{Edge:F3}",
            signal.Asset.Symbol, signal.TimeFrame.Value, signal.Direction,
            signal.FairValue, signal.Edge);

        await Task.CompletedTask;
    }

    public async Task LogTradeOpenedAsync(Trade trade, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.Trades.Add(trade);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Trade opened [DB]: {Engine} {Symbol} {Direction} Size:{Size:F2} Entry:{Entry:F4}",
                trade.Engine, trade.Asset.Symbol, trade.Direction,
                trade.PositionSize, trade.EntryPrice);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist trade opened: {TradeId} {Engine} {Symbol}",
                trade.Id, trade.Engine, trade.Asset.Symbol);
        }
    }

    public async Task LogTradeClosedAsync(Trade trade, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var existing = await db.Trades.FindAsync([trade.Id], ct);
            if (existing is null)
            {
                _logger.LogWarning("Trade not found in DB for close update: {TradeId}", trade.Id);
                return;
            }

            db.Entry(existing).CurrentValues.SetValues(trade);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Trade closed [DB]: {Engine} {Symbol} {Outcome} PnL:{PnL:F2}",
                trade.Engine, trade.Asset.Symbol, trade.Outcome, trade.PnL);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist trade closed: {TradeId} {Engine}",
                trade.Id, trade.Engine);
        }
    }

    public async Task<IReadOnlyList<Trade>> GetOpenTradesAsync(string engineName, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await db.Trades
                .Where(t => t.Engine == engineName && t.Status == TradeStatus.Open)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load open trades for engine: {Engine}", engineName);
            return Array.Empty<Trade>();
        }
    }

    public async Task<PortfolioSnapshotDto?> GetLatestSnapshotAsync(string engineName, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var snapshot = await db.PortfolioSnapshots
                .Where(s => s.Engine == engineName)
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync(ct);

            if (snapshot is null) return null;

            return new PortfolioSnapshotDto(
                snapshot.Balance,
                snapshot.TotalPnL,
                snapshot.TradeCount,
                snapshot.WinRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load latest snapshot for engine: {Engine}", engineName);
            return null;
        }
    }

    public async Task LogSignalWithResultsAsync(
        Signal signal,
        IReadOnlyList<(string engineName, bool accepted, string? rejectionCode, Guid? tradeId)> engineResults,
        CancellationToken ct = default)
    {
        try
        {
            var record = new SignalRecord(
                signal.Asset.Symbol,
                signal.TimeFrame.Value,
                signal.Direction.ToString(),
                signal.FairValue,
                signal.MarketPrice,
                signal.Edge,
                signal.KellyFraction,
                signal.MuEstimate,
                signal.SigmaEstimate,
                signal.Regime.ToString(),
                signal.Score?.FinalScore,
                signal.Indicators.Rsi.Value,
                signal.Indicators.Macd.Histogram,
                signal.Indicators.BullishCount(),
                signal.GeneratedAt);

            foreach (var (engineName, accepted, rejectionCode, tradeId) in engineResults)
            {
                record.EngineResults.Add(new SignalEngineResult(
                    record.Id,
                    engineName,
                    accepted,
                    rejectionCode,
                    tradeId,
                    DateTime.UtcNow));
            }

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.SignalRecords.Add(record);
            await db.SaveChangesAsync(ct);

            _logger.LogDebug(
                "Signal+results persisted: {Symbol}/{TF} {Direction} engines:{Count}",
                signal.Asset.Symbol, signal.TimeFrame.Value, signal.Direction, engineResults.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist signal with results: {Symbol}/{TF}",
                signal.Asset.Symbol, signal.TimeFrame.Value);
        }
    }

    public async Task LogPortfolioSnapshotAsync(Portfolio portfolio, CancellationToken ct = default)
    {
        try
        {
            var snapshot = new PortfolioSnapshot
            {
                Engine            = portfolio.Engine,
                Timestamp         = DateTime.UtcNow,
                Balance           = portfolio.Balance,
                OpenPositionCount = portfolio.OpenPositions.Count,
                TotalExposure     = portfolio.TotalExposure,
                TotalPnL          = portfolio.TotalPnL,
                WinRate           = portfolio.TotalTradeCount > 0 ? (decimal?)portfolio.WinRate : null,
                TradeCount        = portfolio.TotalTradeCount
            };

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.PortfolioSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist portfolio snapshot: {Engine}", portfolio.Engine);
        }
    }
}
