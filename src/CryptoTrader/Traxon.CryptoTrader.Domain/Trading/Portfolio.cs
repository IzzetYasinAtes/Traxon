using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Domain.Trading;

public sealed class Portfolio : AggregateRoot<Guid>
{
    public string Engine { get; }
    public decimal InitialBalance { get; }
    public decimal Balance { get; private set; }
    public int WinCount { get; private set; }
    public int LossCount { get; private set; }
    public int TotalTradeCount => WinCount + LossCount;
    public decimal TotalPnL { get; private set; }
    public decimal WinRate => TotalTradeCount > 0 ? (decimal)WinCount / TotalTradeCount : 0;

    private readonly List<Position> _openPositions = [];
    public IReadOnlyList<Position> OpenPositions => _openPositions.AsReadOnly();

    public decimal TotalExposure => _openPositions.Sum(p => p.PositionSize);
    // TODO: Bu limit tamamen kaldırılabilir ama şimdilik %90 kaldı
    public decimal MaxExposure => Balance * 0.90m;
    public decimal MaxPositionSize => Math.Max(Balance * 0.02m, 1.0m);

    private Portfolio() { Engine = null!; }

    public Portfolio(string engine, decimal initialBalance)
    {
        Id = Guid.NewGuid();
        Engine = engine;
        InitialBalance = initialBalance;
        Balance = initialBalance;
    }

    /// <summary>
    /// Worker restart sonrasi son snapshot'tan portfolio state'i restore eder.
    /// </summary>
    public void Restore(decimal balance, decimal totalPnL, int winCount, int lossCount)
    {
        Balance   = balance;
        TotalPnL  = totalPnL;
        WinCount  = winCount;
        LossCount = lossCount;
    }

    public Result<Position> OpenPosition(Position position)
    {
        if (position.PositionSize > MaxPositionSize)
            return Result<Position>.Failure(Error.PortfolioInsufficient);
        // MaxExposure check removed — Balance check alone is sufficient.
        // Old %90 limit was blocking trades when many positions were open
        // waiting for Gamma API resolution (10-15min), causing 100+ rejections.
        if (Balance < position.PositionSize)
            return Result<Position>.Failure(Error.PortfolioInsufficient);

        _openPositions.Add(position);
        Balance -= position.PositionSize;
        Version++;
        return Result<Position>.Success(position);
    }

    public void ClosePosition(Guid positionId, decimal pnl, TradeOutcome outcome)
    {
        var position = _openPositions.FirstOrDefault(p => p.Id == positionId);
        if (position is null) return;

        _openPositions.Remove(position);
        Balance += position.PositionSize + pnl;
        TotalPnL += pnl;

        if (outcome == TradeOutcome.Win) WinCount++;
        else LossCount++;

        Version++;
    }

    /// <summary>
    /// DB'den gelen realized PnL ile in-memory state'i senkronize eder.
    /// Race condition veya DB write hatalarından kaynaklanan drift'i düzeltir.
    /// </summary>
    public void SyncPnL(decimal dbTotalPnL, int dbWinCount, int dbLossCount)
    {
        TotalPnL = dbTotalPnL;
        WinCount = dbWinCount;
        LossCount = dbLossCount;
        // Recalculate balance from source of truth
        Balance = InitialBalance + dbTotalPnL - TotalExposure;
    }
}
