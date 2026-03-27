using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Signals;

/// <summary>Multi-confirmation ve Black-Scholes fair value kullanan sinyal uretici.</summary>
public sealed class SignalGenerator : ISignalGenerator
{
    private readonly IIndicatorCalculator    _indicatorCalculator;
    private readonly IFairValueCalculator    _fairValueCalculator;
    private readonly IPositionSizer          _positionSizer;
    private readonly ILogger<SignalGenerator> _logger;

    private const int     MinCandlesForSignal = 50;
    private const int     RegimeShortPeriod   = 12;
    private const int     RegimeLongPeriod    = 144;
    private const decimal HighVolMultiplier   = 1.5m;
    private const decimal MinMarketPrice      = 0.30m;
    private const decimal MaxMarketPrice      = 0.60m;
    private const int     MinConfirmations    = 3;
    private const decimal SimulatedBankroll   = 10_000m;

    public SignalGenerator(
        IIndicatorCalculator indicatorCalculator,
        IFairValueCalculator fairValueCalculator,
        IPositionSizer positionSizer,
        ILogger<SignalGenerator> logger)
    {
        _indicatorCalculator = indicatorCalculator;
        _fairValueCalculator = fairValueCalculator;
        _positionSizer       = positionSizer;
        _logger              = logger;
    }

    /// <summary>Candle listesinden sinyal uretir.</summary>
    public Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice)
    {
        // Adim 1 — Minimum candle check
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        // Adim 2 — Market price range check
        if (marketPrice < MinMarketPrice || marketPrice > MaxMarketPrice)
            return Result<Signal>.Failure(Error.InvalidMarketPrice);

        // Adim 3 — Indicator hesapla
        var indicatorsResult = _indicatorCalculator.Calculate(asset, timeFrame, candles);
        if (indicatorsResult.IsFailure)
            return Result<Signal>.Failure(indicatorsResult.Error!);

        var indicators = indicatorsResult.Value!;

        // Adim 4 — Multi-confirmation filter
        var bullishCount = indicators.BullishCount();
        var bearishCount = indicators.BearishCount();

        SignalDirection direction;
        if (bullishCount >= MinConfirmations && bullishCount > bearishCount)
            direction = SignalDirection.Up;
        else if (bearishCount >= MinConfirmations && bearishCount > bullishCount)
            direction = SignalDirection.Down;
        else
            return Result<Signal>.Failure(Error.InsufficientConfirmation);

        // Adim 5 — Fair value hesapla
        var fvResult  = _fairValueCalculator.Calculate(candles, timeFrame);
        var fairValue = fvResult.FairValue;

        // Adim 6 — Fair value direction uyumu check
        if (direction == SignalDirection.Up   && fairValue <= 0.5m)
            return Result<Signal>.Failure(Error.SignalDirectionMismatch);
        if (direction == SignalDirection.Down && fairValue >= 0.5m)
            return Result<Signal>.Failure(Error.SignalDirectionMismatch);

        // Adim 7 — Regime detection
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var regime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        // Adim 8 — Position sizing
        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, SimulatedBankroll);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        // Adim 9 — Signal olustur
        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    fvResult.Mu,
            sigmaEstimate: fvResult.Sigma,
            regime:        regime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal generated: {Symbol}/{Interval} {Direction} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Regime:{Regime}",
            asset.Symbol, timeFrame.Value, direction, fairValue, marketPrice, sizeResult.Edge, regime);

        return Result<Signal>.Success(signal);
    }
}
