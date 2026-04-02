using Traxon.CryptoTrader.Domain.Abstractions;

namespace Traxon.CryptoTrader.Domain.Trading;

/// <summary>
/// Her uretilen sinyal icin kalici DB kaydi. Signal ValueObject'tan farkli olarak
/// EF Core entity'si olarak persist edilir ve engine sonuclariyla iliskilendirilir.
/// </summary>
public sealed class SignalRecord : Entity<Guid>
{
    public string Symbol { get; private set; } = null!;
    public string TimeFrame { get; private set; } = null!;
    public string Direction { get; private set; } = null!;
    public decimal FairValue { get; private set; }
    public decimal MarketPrice { get; private set; }
    public decimal Edge { get; private set; }
    public decimal KellyFraction { get; private set; }
    public decimal MuEstimate { get; private set; }
    public decimal SigmaEstimate { get; private set; }
    public string Regime { get; private set; } = null!;
    public decimal? SignalScore { get; private set; }
    public decimal Rsi { get; private set; }
    public decimal MacdHistogram { get; private set; }
    public int BullishCount { get; private set; }
    public DateTime GeneratedAt { get; private set; }

    /// <summary>Bu sinyale bagli engine sonuclari.</summary>
    public List<SignalEngineResult> EngineResults { get; private set; } = [];

    private SignalRecord() { }

    public SignalRecord(
        string symbol,
        string timeFrame,
        string direction,
        decimal fairValue,
        decimal marketPrice,
        decimal edge,
        decimal kellyFraction,
        decimal muEstimate,
        decimal sigmaEstimate,
        string regime,
        decimal? signalScore,
        decimal rsi,
        decimal macdHistogram,
        int bullishCount,
        DateTime generatedAt)
    {
        Id = Guid.NewGuid();
        Symbol = symbol;
        TimeFrame = timeFrame;
        Direction = direction;
        FairValue = fairValue;
        MarketPrice = marketPrice;
        Edge = edge;
        KellyFraction = kellyFraction;
        MuEstimate = muEstimate;
        SigmaEstimate = sigmaEstimate;
        Regime = regime;
        SignalScore = signalScore;
        Rsi = rsi;
        MacdHistogram = macdHistogram;
        BullishCount = bullishCount;
        GeneratedAt = generatedAt;
    }
}
