using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
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
    private const int     MinBullishConfirmations  = 2;
    private const int     MinBearishConfirmations  = 4; // DOWN trade'ler aktif — Analyst v3 onerisi
    private const decimal SimulatedBankroll        = 10_000m;

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

        // Adim 5 — Fair value hesapla
        var fvResult  = _fairValueCalculator.Calculate(candles, timeFrame);
        var fairValue = fvResult.FairValue;

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

        // Adim 7 — Position sizing
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
            muEstimate:    fvResult.Mu,
            sigmaEstimate: fvResult.Sigma,
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

        // Adim 5 — Fair value hesapla
        var fvResult  = _fairValueCalculator.Calculate(candles, timeFrame);
        var fairValue = fvResult.FairValue;

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

        // Adim 7 — Position sizing
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
            muEstimate:    fvResult.Mu,
            sigmaEstimate: fvResult.Sigma,
            regime:        regime,
            indicators:    indicators);

        _logger.LogInformation(
            "Signal generated (precomputed): {Symbol}/{Interval} {Direction} FV:{FV:F3} Market:{Market:F3} Edge:{Edge:F3} Regime:{Regime} Bulls:{Bulls}/5",
            asset.Symbol, timeFrame.Value, direction, fairValue, marketPrice, sizeResult.Edge, regime, indicators.BullishCount());

        return Result<Signal>.Success(signal);
    }
}
