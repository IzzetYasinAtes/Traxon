using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IIndicatorCalculator
{
    /// <summary>
    /// Verilen candle listesinden tüm indicator'ları hesapla.
    /// Minimum 30 candle gerektirir (Slow SMA için).
    /// </summary>
    Result<TechnicalIndicators> Calculate(
        Asset asset,
        TimeFrame timeFrame,
        IReadOnlyList<Candle> candles);

    Result<RsiResult>            CalculateRsi(IReadOnlyList<decimal> closes, int period = 14);
    Result<MacdResult>           CalculateMacd(IReadOnlyList<decimal> closes, int fast = 12, int slow = 26, int signal = 9);
    Result<BollingerBandsResult> CalculateBollingerBands(IReadOnlyList<decimal> closes, int period = 20, decimal multiplier = 2m);
    Result<AtrResult>            CalculateAtr(IReadOnlyList<Candle> candles, int period = 14);
    Result<VwapResult>           CalculateVwap(IReadOnlyList<Candle> candles, int period = 20);
    Result<StochasticResult>     CalculateStochastic(IReadOnlyList<Candle> candles, int kPeriod = 14, int dPeriod = 3, int smoothK = 3);
    decimal                      CalculateSma(IReadOnlyList<decimal> values, int period);
    decimal                      CalculateParkinsonVolatility(IReadOnlyList<Candle> candles, int period = 14);
}
