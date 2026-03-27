namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class MacdResult : IndicatorResult
{
    public decimal Macd { get; }
    public decimal Signal { get; }
    public decimal Histogram { get; }
    public bool IsBullish => Histogram > 0;

    public MacdResult(decimal macd, decimal signal, decimal histogram)
    {
        Macd = macd;
        Signal = signal;
        Histogram = histogram;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    { yield return Macd; yield return Signal; yield return Histogram; }
}
