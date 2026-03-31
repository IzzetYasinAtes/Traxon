using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Infrastructure.Signals;

/// <summary>Multi-confirmation ve momentum-based fair value kullanan sinyal uretici.</summary>
public sealed class SignalGenerator : ISignalGenerator
{
    private readonly IIndicatorCalculator    _indicatorCalculator;
    private readonly IPositionSizer          _positionSizer;
    private readonly ILogger<SignalGenerator> _logger;

    private const int     MinCandlesForSignal      = 20;
    private const int     RegimeShortPeriod        = 12;
    private const int     RegimeLongPeriod         = 144;
    private const decimal HighVolMultiplier        = 1.5m;
    private const decimal MinMarketPrice           = 0.30m;
    private const decimal MaxMarketPrice           = 0.80m;
    /// <summary>
    /// Kisa vadeli kripto icin asimetrik esik: UP icin 2+ bullish yeterli, DOWN icin
    /// 4+ bearish gerekir. Bu sekilde neutral markette DOWN bias azaltilir.
    /// </summary>
    private const int     MinBullishConfirmations  = 3;
    private const int     MinBearishConfirmations  = 99; // DOWN kapatildi — DOWN trade'ler -$847 zarar (Analyst v4)
    private const decimal SimulatedBankroll        = 10_000m;

    public SignalGenerator(
        IIndicatorCalculator indicatorCalculator,
        IPositionSizer positionSizer,
        ILogger<SignalGenerator> logger)
    {
        _indicatorCalculator = indicatorCalculator;
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

        // Adim 4 — Multi-confirmation filter (asimetrik esik)
        // Kisa vadeli kripto genellikle nötr — DOWN bias'ini onlemek icin UP eigi daha dusuk.
        // UP: 2+ bullish indicator yeterli (net bearish cogunluk gerekmez)
        // DOWN: bullish < 2 (yani bearish >= 4) gerekir
        var bullishCount = indicators.BullishCount();
        var bearishCount = indicators.BearishCount();

        SignalDirection direction;
        if (bullishCount >= MinBullishConfirmations)
            direction = SignalDirection.Up;
        else if (bearishCount >= MinBearishConfirmations)
            direction = SignalDirection.Down;
        else
            return Result<Signal>.Failure(Error.InsufficientConfirmation);

        // Adim 5 — Momentum probability (Black-Scholes yerine)
        var fairValue = indicators.CalculateSignalScore();

        // Adim 5b — Fair value range check (FV tradeable aralikta mi?)
        if (fairValue < MinMarketPrice || fairValue > MaxMarketPrice)
            return Result<Signal>.Failure(Error.FairValueOutOfRange);

        // Adim 5c — UP sinyaller icin FV >= 0.48 zorunlu (Analyst v2 onerisi)
        if (direction == SignalDirection.Up && fairValue < 0.48m)
            return Result<Signal>.Failure(Error.FairValueTooLowForUp);

        // Adim 5d — DOWN sinyaller icin FV <= 0.52 zorunlu (Analyst v3 onerisi)
        if (direction == SignalDirection.Down && fairValue > 0.52m)
            return Result<Signal>.Failure(Error.FairValueTooHighForDown);

        // Adim 6 — Regime detection
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var regime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        // Adim 7 — Position sizing (edge = |score - 0.50|)
        var isLowVol = regime == MarketRegime.LowVolatility;
        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, SimulatedBankroll, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        // Adim 8 — Signal olustur
        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    0m,
            sigmaEstimate: 0m,
            regime:        regime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal generated: {Symbol}/{Interval} {Direction} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Regime:{Regime} Bulls:{Bulls}/5",
            asset.Symbol, timeFrame.Value, direction, fairValue, marketPrice, sizeResult.Edge, regime, indicators.BullishCount());

        return Result<Signal>.Success(signal);
    }

    /// <summary>
    /// Onceden hesaplanmis indicator'lari kullanarak sinyal uretir — cift hesaplamay onler.
    /// Adim 3 (indicator hesaplama) skip edilir.
    /// </summary>
    public Result<Signal> Generate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators precomputedIndicators)
    {
        // Adim 1 — Minimum candle check
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        // Adim 2 — Market price range check
        if (marketPrice < MinMarketPrice || marketPrice > MaxMarketPrice)
            return Result<Signal>.Failure(Error.InvalidMarketPrice);

        // Adim 3 SKIP — precomputedIndicators kullan
        var indicators = precomputedIndicators;

        // Adim 4 — Multi-confirmation filter (asimetrik esik, DOWN bias azaltmak icin)
        var bullishCount = indicators.BullishCount();
        var bearishCount = indicators.BearishCount();

        SignalDirection direction;
        if (bullishCount >= MinBullishConfirmations)
            direction = SignalDirection.Up;
        else if (bearishCount >= MinBearishConfirmations)
            direction = SignalDirection.Down;
        else
            return Result<Signal>.Failure(Error.InsufficientConfirmation);

        // Adim 5 — Momentum probability (Black-Scholes yerine)
        var fairValue = indicators.CalculateSignalScore();

        // Adim 5b — Fair value range check (FV tradeable aralikta mi?)
        if (fairValue < MinMarketPrice || fairValue > MaxMarketPrice)
            return Result<Signal>.Failure(Error.FairValueOutOfRange);

        // Adim 5c — UP sinyaller icin FV >= 0.48 zorunlu (Analyst v2 onerisi)
        if (direction == SignalDirection.Up && fairValue < 0.48m)
            return Result<Signal>.Failure(Error.FairValueTooLowForUp);

        // Adim 5d — DOWN sinyaller icin FV <= 0.52 zorunlu (Analyst v3 onerisi)
        if (direction == SignalDirection.Down && fairValue > 0.52m)
            return Result<Signal>.Failure(Error.FairValueTooHighForDown);

        // Adim 6 — Regime detection
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var regime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        // Adim 7 — Position sizing (edge = |score - 0.50|)
        var isLowVol = regime == MarketRegime.LowVolatility;
        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, SimulatedBankroll, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        // Adim 8 — Signal olustur
        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    0m,
            sigmaEstimate: 0m,
            regime:        regime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal generated (precomputed): {Symbol}/{Interval} {Direction} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Regime:{Regime} Bulls:{Bulls}/5",
            asset.Symbol, timeFrame.Value, direction, fairValue, marketPrice, sizeResult.Edge, regime, indicators.BullishCount());

        return Result<Signal>.Success(signal);
    }

    /// <summary>V2 sinyal motoru: agirlikli skor + 1h trend dogrulama + volume dogrulama.</summary>
    public Result<Signal> GenerateV2(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles,
        decimal marketPrice,
        TechnicalIndicators precomputedIndicators,
        IReadOnlyList<Candle>? hourlyCandles)
    {
        // Adim 1 — Minimum candle check
        if (candles.Count < MinCandlesForSignal)
            return Result<Signal>.Failure(Error.NotEnoughCandles);

        // Adim 2 — Market price range check
        if (marketPrice < MinMarketPrice || marketPrice > MaxMarketPrice)
            return Result<Signal>.Failure(Error.InvalidMarketPrice);

        var indicators = precomputedIndicators;

        // Adim 3 — Agirlikli sinyal skoru hesapla
        var finalScore = indicators.CalculateSignalScore();

        // Adim 4 — Skor bazli yon karari: >0.60 UP, <0.40 DOWN
        SignalDirection direction;
        if (finalScore > 0.60m)
            direction = SignalDirection.Up;
        else if (finalScore < 0.40m)
            direction = SignalDirection.Down;
        else
            return Result<Signal>.Failure(Error.InsufficientConfirmation);

        // Adim 5 — 1h trend dogrulama (hard block: counter-trend sinyal URETME)
        var hourlyTrendConfirmed = false;
        if (hourlyCandles is { Count: >= 10 })
        {
            var hourlyCloses = hourlyCandles.Select(c => c.Close).ToList();
            var hourlySmaFast = _indicatorCalculator.CalculateSma(hourlyCloses, 5);
            var hourlySmaSlow = _indicatorCalculator.CalculateSma(hourlyCloses, 10);

            var hourlyTrendUp = hourlySmaFast > hourlySmaSlow;

            if (direction == SignalDirection.Up && !hourlyTrendUp)
                return Result<Signal>.Failure(Error.CounterTrend);

            if (direction == SignalDirection.Down && hourlyTrendUp)
                return Result<Signal>.Failure(Error.CounterTrend);

            hourlyTrendConfirmed = true;
        }

        // Adim 6 — Volume dogrulama
        var volumeConfirmed = indicators.Volume is { Ratio: >= 0.8m };

        // Adim 7 — Momentum probability (Black-Scholes yerine)
        var fairValue = finalScore;

        if (fairValue < MinMarketPrice || fairValue > MaxMarketPrice)
            return Result<Signal>.Failure(Error.FairValueOutOfRange);

        if (direction == SignalDirection.Up && fairValue < 0.48m)
            return Result<Signal>.Failure(Error.FairValueTooLowForUp);

        if (direction == SignalDirection.Down && fairValue > 0.52m)
            return Result<Signal>.Failure(Error.FairValueTooHighForDown);

        // Adim 8 — Regime detection
        var volShort = _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeShortPeriod);
        var volLong  = candles.Count >= RegimeLongPeriod
            ? _indicatorCalculator.CalculateParkinsonVolatility(candles, RegimeLongPeriod)
            : volShort;

        var regime = (volLong > 0 && volShort > HighVolMultiplier * volLong)
            ? MarketRegime.HighVolatility
            : MarketRegime.LowVolatility;

        // Adim 9 — Position sizing
        var isLowVol = regime == MarketRegime.LowVolatility;
        var sizeResult = _positionSizer.Calculate(fairValue, marketPrice, SimulatedBankroll, isLowVol);
        if (!sizeResult.MeetsMinimumEdge)
            return Result<Signal>.Failure(Error.InvalidEdge);

        // Adim 10 — SignalScore olustur
        var signalScore = new SignalScore(
            FinalScore: finalScore,
            Edge: sizeResult.Edge,
            HourlyTrendConfirmed: hourlyTrendConfirmed,
            VolumeConfirmed: volumeConfirmed,
            IsUpSignal: direction == SignalDirection.Up,
            IsDownSignal: direction == SignalDirection.Down);

        // Adim 11 — Signal olustur
        var signal = new Signal(
            asset:         asset,
            timeFrame:     timeFrame,
            direction:     direction,
            fairValue:     fairValue,
            marketPrice:   marketPrice,
            kellyFraction: sizeResult.KellyFraction,
            muEstimate:    0m,
            sigmaEstimate: 0m,
            regime:        regime,
            indicators:    indicators,
            score:         signalScore);

        _logger.LogInformation(
            "V2 Signal: {Symbol}/{Interval} {Direction} Score:{Score:F3} FV:{FV:F3} Edge:{Edge:F3} " +
            "Trend1h:{Trend} Volume:{Vol} Regime:{Regime}",
            asset.Symbol, timeFrame.Value, direction, finalScore, fairValue, sizeResult.Edge,
            hourlyTrendConfirmed, volumeConfirmed, regime);

        return Result<Signal>.Success(signal);
    }
}
