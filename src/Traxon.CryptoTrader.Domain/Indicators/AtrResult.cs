namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class AtrResult : IndicatorResult
{
    public decimal Value { get; }
    public AtrResult(decimal value) => Value = value;
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
