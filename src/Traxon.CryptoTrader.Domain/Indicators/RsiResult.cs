namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class RsiResult : IndicatorResult
{
    public decimal Value { get; }
    public bool IsOverbought => Value >= 70;
    public bool IsOversold   => Value <= 30;
    public bool IsAboveMiddle => Value > 50;

    public RsiResult(decimal value) => Value = value;
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
