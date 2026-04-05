using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Signals;

/// <summary>
/// Mean-reversion signal generator with Z-Score and contrarian indicator confirmation.
/// Replaces the old momentum-based SignalGenerator.
/// </summary>
public sealed class MeanReversionSignalGenerator : ISignalGenerator
{
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly IPositionSizer       _positionSizer;
    private readonly ILogger<MeanReversionSignalGenerator> _logger;

    private const int     ReturnWindowMinutes       = 120;
    private const int     ReturnPeriodMinutes       = 5;
    private const int     MinCandlesForSignal       = 20;
    private const decimal ZScoreThreshold           = 1.8m;
    private const decimal DecayFactor               = 0.4m;
    private const decimal ConfirmationEdgeReduction = 0.30m;
    private const decimal MinMarketPrice            = 0.30m;
    private const decimal MaxMarketPrice            = 0.80m;
    private const int     IndicatorConfirmThreshold = 3;
    private const int     RegimeShortPeriod         = 60;
    private const int     RegimeLongPeriod          = 720;
    private const decimal HighVolMultiplier         = 1.5m;

    public MeanReversionSignalGenerator(
        IIndicatorCalculator indicatorCalculator,
        IPositionSizer positionSizer,
        ILogger<MeanReversionSignalGenerator> logger)
    {
        _indicatorCalculator = indicatorCalculator;
        _positionSizer       = positionSizer;
        _logger              = logger;
    }

    public Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice)
    {
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        var indicatorsResult = _indicatorCalculator.Calculate(asset, timeFrame, candles);
        if (indicatorsResult.IsFailure)
            return Result<Signal>.Failure(indicatorsResult.Error!);

        return GenerateCore(asset, timeFrame, candles, marketPrice, indicatorsResult.Value!);
    }

    public Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators precomputedIndicators)
    {
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        return GenerateCore(asset, timeFrame, candles, marketPrice, precomputedIndicators);
    }

    private Result<Signal> GenerateCore(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators indicators)
    {
        if (marketPrice < MinMarketPrice || marketPrice > MaxMarketPrice)
            return Result<Signal>.Failure(Error.InvalidMarketPrice);

        // Layer 1: Z-Score mean reversion
        var zScore = ZScoreCalculator.Compute(candles);

        // Only DOWN signals — UP signals disabled (16.7% win rate in live testing)
        SignalDirection direction;
        if (zScore > ZScoreThreshold)
            direction = SignalDirection.Down;
        else
            return Result<Signal>.Failure(Error.InsufficientConfirmation);

        // Layer 2: Indicator confirmation (contrarian)
        var bullishCount = indicators.BullishCount();
        var bearishCount = indicators.BearishCount();

        bool confirmed;
        if (direction == SignalDirection.Down)
            confirmed = bullishCount >= IndicatorConfirmThreshold;
        else
            confirmed = bearishCount >= IndicatorConfirmThreshold;

        // Layer 3: Fair value from Z-Score via NormalCDF
        var fairValue = NormalCdf((double)(-zScore * DecayFactor));
        var fairValueDecimal = Math.Clamp((decimal)fairValue, 0.01m, 0.99m);

        var edge = Math.Abs(fairValueDecimal - marketPrice);
        if (!confirmed)
            edge *= (1m - ConfirmationEdgeReduction);

        // Regime detection
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var regime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        // Position sizing
        var isLowVol = regime == MarketRegime.LowVolatility;

        // Adjust fairValue for position sizing when not confirmed (reduce effective edge)
        var effectiveFairValue = confirmed
            ? fairValueDecimal
            : marketPrice + (fairValueDecimal > marketPrice ? 1 : -1) * edge;

        var sizeResult = _positionSizer.Calculate(effectiveFairValue, marketPrice, 20m, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValueDecimal,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    zScore,
            sigmaEstimate: 0m,
            regime:        regime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal generated: {Symbol}/{Interval} {Direction} Z:{Z:F2} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Confirmed:{Confirmed} Regime:{Regime}",
            asset.Symbol, timeFrame.Value, direction, zScore,
            fairValueDecimal, marketPrice, edge, confirmed, regime);

        return Result<Signal>.Success(signal);
    }

    /// <summary>
    /// Abramowitz and Stegun approximation for the cumulative normal distribution.
    /// Maximum error: 7.5e-8.
    /// </summary>
    private static double NormalCdf(double x)
    {
        const double a1 =  0.254829592;
        const double a2 = -0.284496736;
        const double a3 =  1.421413741;
        const double a4 = -1.453152027;
        const double a5 =  1.061405429;
        const double p  =  0.3275911;

        var sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2.0);

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * y);
    }
}
