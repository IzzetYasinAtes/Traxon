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
    public decimal MaxExposure => Balance * 0.30m;
    public decimal MaxPositionSize => Balance * 0.02m;

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
        if (TotalExposure + position.PositionSize > MaxExposure)
            return Result<Position>.Failure(Error.PortfolioInsufficient);
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
}
