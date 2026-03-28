using Microsoft.EntityFrameworkCore;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Admin.Services;

public sealed class AdminDataService(IDbContextFactory<AppDbContext> dbFactory)
{
    // ---- PERFORMANCE ----

    public async Task<PerformanceSummary> GetPerformanceSummaryAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var trades = await db.Trades
            .Where(t => t.Status == TradeStatus.Closed)
            .ToListAsync();

        if (trades.Count == 0)
            return new PerformanceSummary(0, 0, 0, 0, 0, []);

        var wins = trades.Count(t => t.Outcome == TradeOutcome.Win);
        var totalPnl = trades.Sum(t => t.PnL ?? 0);
        var winRate = (decimal)wins / trades.Count;
        var sharpe = CalculateSharpe(trades.Select(t => (double)(t.PnL ?? 0)).ToList());

        // All registered engines (from PortfolioSnapshots) — even those with 0 trades
        var allEngines = await db.PortfolioSnapshots
            .Select(p => p.Engine)
            .Distinct()
            .ToListAsync();

        var tradesByEngine = trades
            .GroupBy(t => t.Engine)
            .ToDictionary(g => g.Key, g => g.ToList());

        var byEngine = allEngines
            .Select(engine =>
            {
                if (tradesByEngine.TryGetValue(engine, out var engineTrades))
                {
                    var engineWins = engineTrades.Count(t => t.Outcome == TradeOutcome.Win);
                    var enginePnl = engineTrades.Sum(t => t.PnL ?? 0);
                    var engineSharpe = CalculateSharpe(engineTrades.Select(t => (double)(t.PnL ?? 0)).ToList());
                    return new EngineStats(engine, engineTrades.Count, engineWins, enginePnl, engineSharpe);
                }
                return new EngineStats(engine, 0, 0, 0, 0);
            })
            .ToList();

        return new PerformanceSummary(winRate, totalPnl, sharpe, trades.Count, wins, byEngine);
    }

    private static decimal CalculateSharpe(List<double> pnls)
    {
        if (pnls.Count < 2) return 0;
        var mean = pnls.Average();
        var variance = pnls.Select(p => Math.Pow(p - mean, 2)).Average();
        var stddev = Math.Sqrt(variance);
        return stddev > 0 ? (decimal)(mean / stddev * Math.Sqrt(252)) : 0;
    }

    // ---- EQUITY CURVE ----

    public async Task<List<EquityPoint>> GetEquityCurveAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var snapshots = await db.PortfolioSnapshots
            .OrderByDescending(p => p.Timestamp)
            .Take(1000)
            .ToListAsync();
        snapshots.Reverse();
        return snapshots
            .Select(p => new EquityPoint(p.Engine, new DateTimeOffset(p.Timestamp, TimeSpan.Zero), p.Balance))
            .ToList();
    }

    // ---- CALIBRATION ----

    public async Task<List<CalibrationPoint>> GetCalibrationDataAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // EF Core: owned entity projection — materialize first, then project
        var trades = await db.Trades
            .Where(t => t.Status == TradeStatus.Closed)
            .Select(t => new { t.FairValue, t.Outcome })
            .ToListAsync();

        return trades
            .Select(t => new CalibrationPoint(
                (double)t.FairValue,
                t.Outcome == TradeOutcome.Win ? 1.0 : 0.0))
            .ToList();
    }

    public async Task<List<BrierBucket>> GetBrierBucketsAsync()
    {
        var points = await GetCalibrationDataAsync();
        var buckets = new List<BrierBucket>();
        for (int i = 0; i < 10; i++)
        {
            var low = i * 0.1;
            var high = (i + 1) * 0.1;
            var bucket = points.Where(p => p.Predicted >= low && p.Predicted < high).ToList();
            if (bucket.Count == 0) continue;
            var avgPredicted = bucket.Average(p => p.Predicted);
            var actualRate = bucket.Average(p => p.Actual);
            buckets.Add(new BrierBucket($"{low:P0}-{high:P0}", avgPredicted, actualRate, bucket.Count));
        }
        return buckets;
    }

    // ---- TRADE HISTORY ----

    public async Task<List<AdminTradeRow>> GetTradeHistoryAsync(AdminTradeFilter filter)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Materialize first (owned entity navigation), then filter in memory for complex predicates
        var query = db.Trades.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Engine))
            query = query.Where(t => t.Engine == filter.Engine);
        if (filter.Outcome.HasValue)
            query = query.Where(t => t.Outcome == filter.Outcome);
        if (filter.From.HasValue)
            query = query.Where(t => t.OpenedAt >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(t => t.OpenedAt <= filter.To.Value);

        var trades = await query
            .OrderByDescending(t => t.OpenedAt)
            .Skip(filter.Page * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        // Symbol filter in-memory (owned entity)
        var filtered = string.IsNullOrEmpty(filter.Symbol)
            ? trades
            : trades.Where(t => t.Asset.Symbol.Contains(filter.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();

        return filtered.Select(t => new AdminTradeRow(
            t.Id,
            t.Engine,
            t.Asset.Symbol,
            t.TimeFrame.Value,
            t.Direction.ToString(),
            t.EntryPrice,
            t.ExitPrice,
            t.FairValue,
            t.Edge,
            t.PositionSize,
            t.Status.ToString(),
            t.Outcome?.ToString(),
            t.PnL,
            t.OpenedAt,
            t.ClosedAt,
            t.Regime.ToString(),
            t.EntryReason)).ToList();
    }

    public async Task<int> GetTradeCountAsync(AdminTradeFilter filter)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Trades.AsQueryable();
        if (!string.IsNullOrEmpty(filter.Engine))
            query = query.Where(t => t.Engine == filter.Engine);
        if (filter.Outcome.HasValue)
            query = query.Where(t => t.Outcome == filter.Outcome);
        if (filter.From.HasValue)
            query = query.Where(t => t.OpenedAt >= filter.From.Value);
        if (filter.To.HasValue)
            query = query.Where(t => t.OpenedAt <= filter.To.Value);
        return await query.CountAsync();
    }

    // ---- ENGINE STATUS ----

    public async Task<List<EngineStatusRow>> GetEngineStatusAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var snapshots = await db.PortfolioSnapshots.ToListAsync();
        return snapshots
            .GroupBy(p => p.Engine)
            .Select(g =>
            {
                var latest = g.OrderByDescending(p => p.Timestamp).First();
                return new EngineStatusRow(
                    latest.Engine,
                    "Running",
                    latest.Balance,
                    latest.OpenPositionCount,
                    latest.TotalPnL,
                    latest.WinRate ?? 0,
                    latest.TradeCount,
                    latest.Timestamp);
            })
            .ToList();
    }

    // ---- SYSTEM LOGS ----

    public async Task<List<AdminLogEntry>> GetRecentLogsAsync(int count = 200)
    {
        var logDir = "logs";
        if (!Directory.Exists(logDir)) return [];

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var logFile = Path.Combine(logDir, $"admin-{today}.log");
        if (!File.Exists(logFile)) return [];

        await using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        var content = await reader.ReadToEndAsync();
        var lines = content.Split(Environment.NewLine);
        return lines
            .TakeLast(count)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(ParseLogLine)
            .ToList();
    }

    private static AdminLogEntry ParseLogLine(string line)
    {
        // Serilog default output template: "[HH:mm:ss INF] Message"
        // Just return raw line with minimal parsing
        var level = "INF";
        if (line.Contains(" [ERR] ") || line.Contains(" [FTL] ")) level = "ERR";
        else if (line.Contains(" [WRN] ")) level = "WRN";
        else if (line.Contains(" [DBG] ")) level = "DBG";
        return new AdminLogEntry(DateTime.UtcNow, level, line);
    }
}

// ---- Value Record Types ----

public record PerformanceSummary(
    decimal WinRate,
    decimal TotalPnL,
    decimal SharpeRatio,
    int TotalTrades,
    int WinCount,
    List<EngineStats> ByEngine);

public record EngineStats(
    string Engine,
    int Trades,
    int Wins,
    decimal TotalPnL,
    decimal Sharpe);

public record EquityPoint(string Engine, DateTimeOffset Timestamp, decimal Balance);
public record CalibrationPoint(double Predicted, double Actual);
public record BrierBucket(string Label, double AvgPredicted, double ActualRate, int Count);

public record AdminTradeRow(
    Guid Id,
    string Engine,
    string Symbol,
    string Interval,
    string Direction,
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal FairValue,
    decimal Edge,
    decimal PositionSize,
    string Status,
    string? Outcome,
    decimal? PnL,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string Regime,
    string EntryReason);

public record EngineStatusRow(
    string Engine,
    string Status,
    decimal Balance,
    int OpenPositions,
    decimal TotalPnL,
    decimal WinRate,
    int TradeCount,
    DateTime LastUpdated);

public record AdminTradeFilter
{
    public string? Engine { get; init; }
    public string? Symbol { get; init; }
    public TradeOutcome? Outcome { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int Page { get; init; } = 0;
    public int PageSize { get; init; } = 50;
}

public record AdminLogEntry(DateTime Timestamp, string Level, string Message);
