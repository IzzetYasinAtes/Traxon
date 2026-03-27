using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Indicators;

namespace Traxon.CryptoTrader.Domain.Trading;

public enum SignalDirection { Up, Down }
public enum MarketRegime { LowVolatility, HighVolatility }

public sealed class Signal : ValueObject
{
    public Guid SignalId { get; }
    public Asset Asset { get; }
    public TimeFrame TimeFrame { get; }
    public SignalDirection Direction { get; }

    /// <summary>Fair value P(Up) — Φ(d2)</summary>
    public decimal FairValue { get; }

    /// <summary>Market price (Polymarket YES token price)</summary>
    public decimal MarketPrice { get; }

    /// <summary>Edge = |FairValue - MarketPrice|</summary>
    public decimal Edge => Math.Abs(FairValue - MarketPrice);

    /// <summary>Half Kelly fraction</summary>
    public decimal KellyFraction { get; }

    /// <summary>Momentum (μ) — EMA of log returns</summary>
    public decimal MuEstimate { get; }

    /// <summary>Volatility (σ) — Parkinson estimator</summary>
    public decimal SigmaEstimate { get; }

    public MarketRegime Regime { get; }
    public TechnicalIndicators Indicators { get; }
    public DateTime GeneratedAt { get; }

    public Signal(
        Asset asset,
        TimeFrame timeFrame,
        SignalDirection direction,
        decimal fairValue,
        decimal marketPrice,
        decimal kellyFraction,
        decimal muEstimate,
        decimal sigmaEstimate,
        MarketRegime regime,
        TechnicalIndicators indicators)
    {
        SignalId = Guid.NewGuid();
        Asset = asset;
        TimeFrame = timeFrame;
        Direction = direction;
        FairValue = fairValue;
        MarketPrice = marketPrice;
        KellyFraction = kellyFraction;
        MuEstimate = muEstimate;
        SigmaEstimate = sigmaEstimate;
        Regime = regime;
        Indicators = indicators;
        GeneratedAt = DateTime.UtcNow;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    { yield return SignalId; }
}
