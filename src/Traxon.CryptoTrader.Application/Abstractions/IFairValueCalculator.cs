using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public record FairValueResult(
    decimal FairValue,
    decimal Mu,
    decimal Sigma,
    decimal D2);

public interface IFairValueCalculator
{
    /// <summary>
    /// P(Up) = Φ(μ√T / σ) hesaplar.
    /// μ = EMA of log returns (12 period)
    /// σ = Parkinson volatility (14 period), annualized
    /// T = timeFrame duration in years
    /// </summary>
    FairValueResult Calculate(
        IReadOnlyList<Candle> candles,
        TimeFrame timeFrame);

    /// <summary>EMA of log returns hesaplar (momentum proxy)</summary>
    decimal CalculateMomentum(IReadOnlyList<decimal> closes, int period = 12);
}
