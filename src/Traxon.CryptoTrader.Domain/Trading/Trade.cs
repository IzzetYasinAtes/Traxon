using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Domain.Trading;

public enum TradeStatus { Open, Closed }
public enum TradeOutcome { Win, Loss }

public sealed class Trade : Entity<Guid>
{
    public string Engine { get; }
    public Asset Asset { get; }
    public TimeFrame TimeFrame { get; }
    public SignalDirection Direction { get; }
    public decimal EntryPrice { get; }
    public decimal? ExitPrice { get; private set; }
    public decimal FairValue { get; }
    public decimal Edge { get; }
    public decimal PositionSize { get; }
    public decimal KellyFraction { get; }
    public decimal MuEstimate { get; }
    public decimal SigmaEstimate { get; }
    public MarketRegime Regime { get; }
    public string IndicatorSnapshot { get; }
    public string EntryReason { get; }
    public DateTime OpenedAt { get; }
    public DateTime? ClosedAt { get; private set; }
    public TradeStatus Status { get; private set; }
    public TradeOutcome? Outcome { get; private set; }
    public decimal? PnL { get; private set; }

    private Trade() { Engine = null!; Asset = null!; TimeFrame = null!; IndicatorSnapshot = null!; EntryReason = null!; }

    public Trade(
        string engine,
        Asset asset,
        TimeFrame timeFrame,
        SignalDirection direction,
        decimal entryPrice,
        decimal fairValue,
        decimal edge,
        decimal positionSize,
        decimal kellyFraction,
        decimal muEstimate,
        decimal sigmaEstimate,
        MarketRegime regime,
        string indicatorSnapshot,
        string entryReason)
    {
        Id = Guid.NewGuid();
        Engine = engine;
        Asset = asset;
        TimeFrame = timeFrame;
        Direction = direction;
        EntryPrice = entryPrice;
        FairValue = fairValue;
        Edge = edge;
        PositionSize = positionSize;
        KellyFraction = kellyFraction;
        MuEstimate = muEstimate;
        SigmaEstimate = sigmaEstimate;
        Regime = regime;
        IndicatorSnapshot = indicatorSnapshot;
        EntryReason = entryReason;
        OpenedAt = DateTime.UtcNow;
        Status = TradeStatus.Open;
    }

    public void Close(decimal exitPrice, TradeOutcome outcome, decimal pnl)
    {
        ExitPrice = exitPrice;
        Outcome = outcome;
        PnL = pnl;
        ClosedAt = DateTime.UtcNow;
        Status = TradeStatus.Closed;
        AddDomainEvent(new Events.TradeClosedEvent(this, outcome.ToString()));
    }
}
