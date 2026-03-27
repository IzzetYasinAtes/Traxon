using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Calculators;

public sealed class IndicatorCalculator : IIndicatorCalculator
{
    private readonly ILogger<IndicatorCalculator> _logger;

    public IndicatorCalculator(ILogger<IndicatorCalculator> logger) =>
        _logger = logger;

    public Result<TechnicalIndicators> Calculate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 30)
            return Result<TechnicalIndicators>.Failure(Error.NotEnoughCandles);

        var closes = candles.Select(c => c.Close).ToList();
        var currentPrice = closes[^1];

        var rsiResult   = CalculateRsi(closes);
        var macdResult  = CalculateMacd(closes);
        var bbResult    = CalculateBollingerBands(closes);
        var atrResult   = CalculateAtr(candles);
        var vwapResult  = CalculateVwap(candles);
        var stochResult = CalculateStochastic(candles);
        var fastSma     = CalculateSma(closes, 10);
        var slowSma     = CalculateSma(closes, 30);
        var parkinson   = CalculateParkinsonVolatility(candles);

        if (rsiResult.IsFailure)   return Result<TechnicalIndicators>.Failure(rsiResult.Error!);
        if (macdResult.IsFailure)  return Result<TechnicalIndicators>.Failure(macdResult.Error!);
        if (bbResult.IsFailure)    return Result<TechnicalIndicators>.Failure(bbResult.Error!);
        if (atrResult.IsFailure)   return Result<TechnicalIndicators>.Failure(atrResult.Error!);
        if (vwapResult.IsFailure)  return Result<TechnicalIndicators>.Failure(vwapResult.Error!);
        if (stochResult.IsFailure) return Result<TechnicalIndicators>.Failure(stochResult.Error!);

        var indicators = new TechnicalIndicators(
            asset: asset,
            timeFrame: timeFrame,
            calculatedAt: DateTime.UtcNow,
            currentPrice: currentPrice,
            rsi: rsiResult.Value!,
            macd: macdResult.Value!,
            bollingerBands: bbResult.Value!,
            atr: atrResult.Value!,
            vwap: vwapResult.Value!,
            stochastic: stochResult.Value!,
            fastSma: fastSma,
            slowSma: slowSma,
            parkinsonVolatility: parkinson);

        return Result<TechnicalIndicators>.Success(indicators);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RSI(14) — Wilder's smoothed RSI
    // ─────────────────────────────────────────────────────────────────────────
    public Result<RsiResult> CalculateRsi(IReadOnlyList<decimal> closes, int period = 14)
    {
        if (closes.Count < period + 1)
            return Result<RsiResult>.Failure(Error.NotEnoughCandles);

        decimal avgGain = 0, avgLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0) avgGain += change;
            else             avgLoss += Math.Abs(change);
        }
        avgGain /= period;
        avgLoss /= period;

        for (int i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change >= 0 ? change : 0;
            var loss = change < 0 ? Math.Abs(change) : 0;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }

        if (avgLoss == 0) return Result<RsiResult>.Success(new RsiResult(100m));

        var rs  = avgGain / avgLoss;
        var rsi = 100m - (100m / (1m + rs));

        return Result<RsiResult>.Success(new RsiResult(Math.Round(rsi, 4)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MACD(12, 26, 9)
    // ─────────────────────────────────────────────────────────────────────────
    public Result<MacdResult> CalculateMacd(IReadOnlyList<decimal> closes, int fast = 12, int slow = 26, int signal = 9)
    {
        if (closes.Count < slow + signal)
            return Result<MacdResult>.Failure(Error.NotEnoughCandles);

        var ema12 = CalculateEmaArray(closes, fast);
        var ema26 = CalculateEmaArray(closes, slow);

        var macdLine = new decimal[ema26.Length];
        int offset = ema12.Length - ema26.Length;
        for (int i = 0; i < ema26.Length; i++)
            macdLine[i] = ema12[i + offset] - ema26[i];

        var signalLine = CalculateEmaArray(macdLine, signal);

        var macdVal   = macdLine[^1];
        var signalVal = signalLine[^1];
        var histogram = macdVal - signalVal;

        return Result<MacdResult>.Success(new MacdResult(
            Math.Round(macdVal, 8),
            Math.Round(signalVal, 8),
            Math.Round(histogram, 8)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bollinger Bands(20, 2)
    // ─────────────────────────────────────────────────────────────────────────
    public Result<BollingerBandsResult> CalculateBollingerBands(
        IReadOnlyList<decimal> closes, int period = 20, decimal multiplier = 2m)
    {
        if (closes.Count < period)
            return Result<BollingerBandsResult>.Failure(Error.NotEnoughCandles);

        var last     = closes.TakeLast(period).ToList();
        var sma      = last.Average();
        var variance = last.Select(c => (c - sma) * (c - sma)).Average();
        var stdDev   = (decimal)Math.Sqrt((double)variance);

        return Result<BollingerBandsResult>.Success(new BollingerBandsResult(
            upper:  Math.Round(sma + multiplier * stdDev, 8),
            middle: Math.Round(sma, 8),
            lower:  Math.Round(sma - multiplier * stdDev, 8)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ATR(14) — Wilder's smoothing
    // ─────────────────────────────────────────────────────────────────────────
    public Result<AtrResult> CalculateAtr(IReadOnlyList<Candle> candles, int period = 14)
    {
        if (candles.Count < period + 1)
            return Result<AtrResult>.Failure(Error.NotEnoughCandles);

        var trList = new List<decimal>();
        for (int i = 1; i < candles.Count; i++)
        {
            var high      = candles[i].High;
            var low       = candles[i].Low;
            var prevClose = candles[i - 1].Close;
            var tr = Math.Max(high - low,
                     Math.Max(Math.Abs(high - prevClose),
                              Math.Abs(low - prevClose)));
            trList.Add(tr);
        }

        var atr = trList.Take(period).Average();
        for (int i = period; i < trList.Count; i++)
            atr = (atr * (period - 1) + trList[i]) / period;

        return Result<AtrResult>.Success(new AtrResult(Math.Round(atr, 8)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VWAP(20) — Rolling 20 candle
    // ─────────────────────────────────────────────────────────────────────────
    public Result<VwapResult> CalculateVwap(IReadOnlyList<Candle> candles, int period = 20)
    {
        if (candles.Count < period)
            return Result<VwapResult>.Failure(Error.NotEnoughCandles);

        var last = candles.TakeLast(period).ToList();
        var tpv  = last.Sum(c => c.Typical * c.Volume);
        var vol  = last.Sum(c => c.Volume);

        if (vol == 0) return Result<VwapResult>.Failure(Error.InvalidCandle);

        return Result<VwapResult>.Success(new VwapResult(Math.Round(tpv / vol, 8)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stochastic(14, 3, 3)
    // ─────────────────────────────────────────────────────────────────────────
    public Result<StochasticResult> CalculateStochastic(
        IReadOnlyList<Candle> candles, int kPeriod = 14, int dPeriod = 3, int smoothK = 3)
    {
        int required = kPeriod + dPeriod + smoothK - 2;
        if (candles.Count < required)
            return Result<StochasticResult>.Failure(Error.NotEnoughCandles);

        var rawK = new List<decimal>();
        for (int i = kPeriod - 1; i < candles.Count; i++)
        {
            var window  = candles.Skip(i - kPeriod + 1).Take(kPeriod).ToList();
            var highest = window.Max(c => c.High);
            var lowest  = window.Min(c => c.Low);
            var current = candles[i].Close;
            rawK.Add(highest == lowest ? 50m : 100m * (current - lowest) / (highest - lowest));
        }

        var smoothedK = new List<decimal>();
        for (int i = smoothK - 1; i < rawK.Count; i++)
            smoothedK.Add(rawK.Skip(i - smoothK + 1).Take(smoothK).Average());

        var dValues = new List<decimal>();
        for (int i = dPeriod - 1; i < smoothedK.Count; i++)
            dValues.Add(smoothedK.Skip(i - dPeriod + 1).Take(dPeriod).Average());

        return Result<StochasticResult>.Success(new StochasticResult(
            k: Math.Round(smoothedK[^1], 4),
            d: Math.Round(dValues[^1], 4)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SMA
    // ─────────────────────────────────────────────────────────────────────────
    public decimal CalculateSma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period) return 0;
        return values.TakeLast(period).Average();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parkinson Volatility: σ = sqrt(1/(4n*ln2) * Σ(ln(H/L))²)
    // ─────────────────────────────────────────────────────────────────────────
    public decimal CalculateParkinsonVolatility(IReadOnlyList<Candle> candles, int period = 14)
    {
        if (candles.Count < period) return 0;

        var last = candles.TakeLast(period).ToList();
        var sumSquaredLogs = last.Sum(c =>
        {
            var ratio    = (double)(c.High / c.Low);
            var logRatio = Math.Log(ratio);
            return logRatio * logRatio;
        });

        var parkinson = Math.Sqrt(sumSquaredLogs / (4.0 * period * Math.Log(2.0)));
        return (decimal)parkinson;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static decimal[] CalculateEmaArray(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period) return [];

        var result = new decimal[values.Count - period + 1];
        var k = 2m / (period + 1);

        result[0] = values.Take(period).Average();

        for (int i = period; i < values.Count; i++)
            result[i - period + 1] = values[i] * k + result[i - period] * (1 - k);

        return result;
    }
}
