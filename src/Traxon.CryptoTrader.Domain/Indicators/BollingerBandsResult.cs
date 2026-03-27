namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class BollingerBandsResult : IndicatorResult
{
    public decimal Upper { get; }
    public decimal Middle { get; }
    public decimal Lower { get; }
    public decimal Bandwidth => Upper - Lower;

    public bool IsPriceAboveMiddle(decimal price) => price > Middle;
    public bool IsPriceNearUpper(decimal price)   => price >= Upper * 0.98m;
    public bool IsPriceNearLower(decimal price)   => price <= Lower * 1.02m;

    public BollingerBandsResult(decimal upper, decimal middle, decimal lower)
    { Upper = upper; Middle = middle; Lower = lower; }

    protected override IEnumerable<object?> GetEqualityComponents()
    { yield return Upper; yield return Middle; yield return Lower; }
}
