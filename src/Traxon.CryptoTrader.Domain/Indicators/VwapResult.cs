namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class VwapResult : IndicatorResult
{
    public decimal Value { get; }
    public bool IsPriceAbove(decimal price) => price > Value;

    public VwapResult(decimal value) => Value = value;
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
