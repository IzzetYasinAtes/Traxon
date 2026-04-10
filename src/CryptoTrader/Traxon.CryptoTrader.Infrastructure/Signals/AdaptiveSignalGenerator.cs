using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Signals;

/// <summary>
/// Adaptive regime-switching signal generator with Hurst-based regime detection.
/// Generates both UP and DOWN signals depending on market regime.
/// Layer 1: Regime Detection (Hurst exponent)
/// Layer 2: Direction Signal (regime-dependent: mean reversion or momentum)
/// Layer 3: Confirmation Filters (indicators, volume, taker ratio)
/// </summary>
public sealed class AdaptiveSignalGenerator : ISignalGenerator
{
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly IPositionSizer       _positionSizer;
    private readonly ILogger<AdaptiveSignalGenerator> _logger;

    // Candle requirements
    private const int MinCandlesForSignal = 30;

    // Hurst regime thresholds
    private const decimal HurstMeanRevertingMax = 0.45m;
    private const decimal HurstTrendingMin      = 0.55m;

    // Mean reversion Z-Score thresholds (asymmetric — DOWN reversion is stronger)
    private const decimal MeanReversionDownThreshold = 1.6m;
    private const decimal MeanReversionUpThreshold   = 2.0m;
    private const decimal UpConfidenceMultiplier      = 0.8m;

    // Momentum / taker ratio thresholds
    private const decimal TakerBullishThreshold = 0.55m;
    private const decimal TakerBearishThreshold = 0.45m;

    // Volume filter
    private const decimal MinVolumeRatio = 0.5m;

    // Fair value and confidence
    private const decimal ConfidenceToFairValueScale    = 0.30m;
    private const decimal UnconfirmedConfidenceReduction = 0.7m;
    private const decimal MinEdge                        = 0.10m;

    // Indicator confirmation
    private const int IndicatorConfirmThreshold = 3;

    // Market price bounds
    private const decimal MinMarketPrice = 0.10m;
    private const decimal MaxMarketPrice = 0.90m;

    // Parkinson volatility regime (secondary)
    private const int     RegimeShortPeriod  = 60;
    private const int     RegimeLongPeriod   = 720;
    private const decimal HighVolMultiplier  = 1.5m;

    // Time-of-day filter (UTC hours to skip)

    public AdaptiveSignalGenerator(
        IIndicatorCalculator indicatorCalculator,
        IPositionSizer positionSizer,
        ILogger<AdaptiveSignalGenerator> logger)
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

    public Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators precomputedIndicators,
        SignalDirection direction)
    {
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        return GenerateWithDirection(asset, timeFrame, candles, marketPrice, precomputedIndicators, direction);
    }

    public Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators precomputedIndicators,
        SignalDirection direction,
        decimal effectiveDelta)
    {
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        return GenerateFromEnsemble(asset, timeFrame, candles, marketPrice, precomputedIndicators, direction, effectiveDelta);
    }

    /// <summary>
    /// Ensemble scoring signal generation. Direction + delta from worker's composite score.
    /// No regime gate — confidence from delta magnitude.
    /// </summary>
    private Result<Signal> GenerateFromEnsemble(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators indicators,
        SignalDirection direction,
        decimal effectiveDelta)
    {
        if (marketPrice < MinMarketPrice || marketPrice > MaxMarketPrice)
            return Result<Signal>.Failure(Error.InvalidMarketPrice);

        // Confidence from composite score magnitude
        var confidence = Math.Clamp(0.50m + Math.Abs(effectiveDelta) * 3m, 0.52m, 0.75m);

        // Fair value: P(Up) basis
        decimal fairValue;
        if (direction == SignalDirection.Up)
            fairValue = confidence;
        else
            fairValue = 1m - confidence;

        fairValue = Math.Clamp(fairValue, 0.01m, 0.99m);

        var edge = Math.Abs(fairValue - marketPrice);
        if (edge < 0.01m)
        {
            _logger.LogDebug("Edge too small for {Symbol}: {Edge:F3}", asset.Symbol, edge);
            return Result<Signal>.Failure(Error.InvalidEdge);
        }

        // Parkinson volatility regime for sizing
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var volRegime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        var isLowVol = volRegime == MarketRegime.LowVolatility;

        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, 30m, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    effectiveDelta,
            sigmaEstimate: 0m,
            regime:        volRegime,
            indicators:    indicators);

        _logger.LogInformation(
            "EnsembleSignal: {Symbol} {Direction} Delta:{Delta:F4} Conf:{Conf:F2} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3}",
            asset.Symbol, direction, effectiveDelta, confidence, fairValue, marketPrice, edge);

        return Result<Signal>.Success(signal);
    }

    /// <summary>
    /// Signal generation with pre-determined direction from worker.
    /// Skips Layer 1 (regime) and Layer 2 (direction) — only does validation, confirmation, sizing.
    /// </summary>
    private Result<Signal> GenerateWithDirection(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators indicators,
        SignalDirection direction)
    {
        if (marketPrice < MinMarketPrice || marketPrice > MaxMarketPrice)
            return Result<Signal>.Failure(Error.InvalidMarketPrice);

        // Compute regime info for logging and indicator confirmation logic
        var hurst = HurstCalculator.Compute(candles, 120);
        var isMeanReverting = hurst < HurstMeanRevertingMax;
        var regimeLabel = isMeanReverting ? "MeanReverting" : "Trending";

        var zScore     = ZScoreCalculator.Compute(candles);
        var takerRatio = TakerRatioCalculator.Compute(candles, 5);

        // Compute confidence based on regime (same logic, but direction is already decided)
        var confidence = 0m;
        if (isMeanReverting)
        {
            confidence = Math.Min(Math.Abs(zScore) / 3.0m, 1.0m);
            if (direction == SignalDirection.Up)
                confidence *= UpConfidenceMultiplier;
        }
        else
        {
            if (direction == SignalDirection.Up)
                confidence = Math.Min((takerRatio - 0.50m) * 4m, 1.0m);
            else
                confidence = Math.Min((0.50m - takerRatio) * 4m, 1.0m);
        }

        confidence = Math.Max(confidence, 0.01m); // Floor to avoid zero

        // ── LAYER 3: Confirmation Filters ──

        // Volume filter: skip dead markets
        var volumeRatio = ComputeVolumeRatio(candles, 5, 20);
        if (volumeRatio < MinVolumeRatio)
        {
            _logger.LogDebug("Dead market for {Symbol}: volume ratio {VR:F2} < {Min:F2}",
                asset.Symbol, volumeRatio, MinVolumeRatio);
            return Result<Signal>.Failure(Error.InsufficientConfirmation);
        }

        // Indicator confirmation
        var bullishCount = indicators.BullishCount();
        var bearishCount = indicators.BearishCount();

        bool indicatorConfirmed;
        if (isMeanReverting)
        {
            indicatorConfirmed = (direction == SignalDirection.Down && bullishCount >= IndicatorConfirmThreshold)
                              || (direction == SignalDirection.Up && bearishCount >= IndicatorConfirmThreshold);
        }
        else
        {
            indicatorConfirmed = (direction == SignalDirection.Up && bullishCount >= IndicatorConfirmThreshold)
                              || (direction == SignalDirection.Down && bearishCount >= IndicatorConfirmThreshold);
        }

        if (!indicatorConfirmed)
            confidence *= UnconfirmedConfidenceReduction;

        // ── LAYER 4: Fair Value and Position Sizing ──
        decimal fairValue;
        if (direction == SignalDirection.Down)
            fairValue = 0.50m - (confidence * ConfidenceToFairValueScale);
        else
            fairValue = 0.50m + (confidence * ConfidenceToFairValueScale);

        fairValue = Math.Clamp(fairValue, 0.01m, 0.99m);

        var edge = Math.Abs(fairValue - marketPrice);
        if (edge < MinEdge)
        {
            _logger.LogDebug("Edge too small for {Symbol}: {Edge:F3} < {Min:F2}", asset.Symbol, edge, MinEdge);
            return Result<Signal>.Failure(Error.InvalidEdge);
        }

        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var volRegime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        var isLowVol = volRegime == MarketRegime.LowVolatility;

        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, 20m, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    zScore,
            sigmaEstimate: 0m,
            regime:        volRegime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal: {Symbol} {Direction} Regime:{Regime} H:{Hurst:F3} Z:{Z:F2} Taker:{Taker:F3} Conf:{Conf:F2} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Confirmed:{Confirmed}",
            asset.Symbol, direction, regimeLabel, hurst, zScore,
            takerRatio, confidence, fairValue, marketPrice, edge, indicatorConfirmed);

        return Result<Signal>.Success(signal);
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

        // ── LAYER 1: Regime Detection (Hurst Exponent) ──
        var hurst = HurstCalculator.Compute(candles, 120);

        if (hurst >= HurstMeanRevertingMax && hurst <= HurstTrendingMin)
        {
            _logger.LogDebug("Random walk regime (H={Hurst:F3}) for {Symbol}, skipping", hurst, asset.Symbol);
            return Result<Signal>.Failure(Error.InsufficientConfirmation);
        }

        var isMeanReverting = hurst < HurstMeanRevertingMax;
        var regimeLabel = isMeanReverting ? "MeanReverting" : "Trending";

        // ── LAYER 2: Direction Signal (regime-dependent) ──
        var zScore     = ZScoreCalculator.Compute(candles);
        var takerRatio = TakerRatioCalculator.Compute(candles, 5);

        SignalDirection? direction = null;
        var confidence = 0m;

        if (isMeanReverting)
        {
            // Mean reversion: extreme Z-Score → bet on reversal
            if (zScore > MeanReversionDownThreshold)
            {
                direction = SignalDirection.Down;
                confidence = Math.Min(Math.Abs(zScore) / 3.0m, 1.0m);
            }
            else if (zScore < -MeanReversionUpThreshold)
            {
                direction = SignalDirection.Up;
                confidence = Math.Min(Math.Abs(zScore) / 3.0m, 1.0m) * UpConfidenceMultiplier;
            }
        }
        else
        {
            // Trending: momentum + taker ratio confirmation
            var recentMomentum = ComputeRecentMomentum(candles, 3);

            if (recentMomentum > 0 && takerRatio > TakerBullishThreshold)
            {
                direction = SignalDirection.Up;
                confidence = Math.Min((takerRatio - 0.50m) * 4m, 1.0m);
            }
            else if (recentMomentum < 0 && takerRatio < TakerBearishThreshold)
            {
                direction = SignalDirection.Down;
                confidence = Math.Min((0.50m - takerRatio) * 4m, 1.0m);
            }
        }

        if (direction is null)
        {
            _logger.LogDebug("No clear signal for {Symbol}: Regime={Regime} H={Hurst:F3} Z={Z:F2} Taker={Taker:F3}",
                asset.Symbol, regimeLabel, hurst, zScore, takerRatio);
            return Result<Signal>.Failure(Error.InsufficientConfirmation);
        }

        // ── LAYER 3: Confirmation Filters ──

        // Volume filter: skip dead markets
        var volumeRatio = ComputeVolumeRatio(candles, 5, 20);
        if (volumeRatio < MinVolumeRatio)
        {
            _logger.LogDebug("Dead market for {Symbol}: volume ratio {VR:F2} < {Min:F2}",
                asset.Symbol, volumeRatio, MinVolumeRatio);
            return Result<Signal>.Failure(Error.InsufficientConfirmation);
        }

        // Indicator confirmation
        var bullishCount = indicators.BullishCount();
        var bearishCount = indicators.BearishCount();

        bool indicatorConfirmed;
        if (isMeanReverting)
        {
            // Contrarian: bullish indicators confirm DOWN signal (overbought → reversal)
            indicatorConfirmed = (direction == SignalDirection.Down && bullishCount >= IndicatorConfirmThreshold)
                              || (direction == SignalDirection.Up && bearishCount >= IndicatorConfirmThreshold);
        }
        else
        {
            // Aligned: bullish indicators confirm UP signal (trend continuation)
            indicatorConfirmed = (direction == SignalDirection.Up && bullishCount >= IndicatorConfirmThreshold)
                              || (direction == SignalDirection.Down && bearishCount >= IndicatorConfirmThreshold);
        }

        if (!indicatorConfirmed)
            confidence *= UnconfirmedConfidenceReduction;

        // ── LAYER 4: Fair Value and Position Sizing ──
        decimal fairValue;
        if (direction == SignalDirection.Down)
            fairValue = 0.50m - (confidence * ConfidenceToFairValueScale);
        else
            fairValue = 0.50m + (confidence * ConfidenceToFairValueScale);

        fairValue = Math.Clamp(fairValue, 0.01m, 0.99m);

        // Edge check
        var edge = Math.Abs(fairValue - marketPrice);
        if (edge < MinEdge)
        {
            _logger.LogDebug("Edge too small for {Symbol}: {Edge:F3} < {Min:F2}", asset.Symbol, edge, MinEdge);
            return Result<Signal>.Failure(Error.InvalidEdge);
        }

        // Parkinson volatility regime (secondary — used for position sizing)
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var volRegime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        var isLowVol = volRegime == MarketRegime.LowVolatility;

        // Position sizing via Kelly criterion
        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, 20m, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction.Value,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    zScore,
            sigmaEstimate: 0m,
            regime:        volRegime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal: {Symbol} {Direction} Regime:{Regime} H:{Hurst:F3} Z:{Z:F2} Taker:{Taker:F3} Conf:{Conf:F2} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Confirmed:{Confirmed}",
            asset.Symbol, direction.Value, regimeLabel, hurst, zScore,
            takerRatio, confidence, fairValue, marketPrice, edge, indicatorConfirmed);

        return Result<Signal>.Success(signal);
    }

    /// <summary>
    /// Computes average return of the last N candles.
    /// Positive = bullish momentum, negative = bearish.
    /// </summary>
    private static decimal ComputeRecentMomentum(IReadOnlyList<Candle> candles, int lookback)
    {
        if (candles.Count < lookback + 1)
            return 0m;

        var sum = 0m;
        var startIdx = candles.Count - lookback;

        for (var i = startIdx; i < candles.Count; i++)
        {
            var prev = candles[i - 1].Close;
            if (prev > 0)
                sum += (candles[i].Close - prev) / prev;
        }

        return sum / lookback;
    }

    /// <summary>
    /// Computes ratio of short-term average volume to long-term average volume.
    /// Values below 0.5 indicate a "dead" market with abnormally low activity.
    /// </summary>
    private static decimal ComputeVolumeRatio(IReadOnlyList<Candle> candles, int shortPeriod, int longPeriod)
    {
        if (candles.Count < longPeriod)
            return 1.0m; // Not enough data — assume OK

        var shortStart = candles.Count - shortPeriod;
        var longStart  = candles.Count - longPeriod;

        var shortSum = 0m;
        for (var i = shortStart; i < candles.Count; i++)
            shortSum += candles[i].Volume;

        var longSum = 0m;
        for (var i = longStart; i < candles.Count; i++)
            longSum += candles[i].Volume;

        var shortAvg = shortSum / shortPeriod;
        var longAvg  = longSum / longPeriod;

        if (longAvg <= 0m)
            return 1.0m;

        return shortAvg / longAvg;
    }
}
