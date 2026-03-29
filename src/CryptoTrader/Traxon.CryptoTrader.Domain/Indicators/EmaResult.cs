namespace Traxon.CryptoTrader.Domain.Indicators;

/// <summary>EMA hesaplama sonucu — deger, egim ve yon bilgisi.</summary>
public sealed class EmaResult : IndicatorResult
{
    /// <summary>EMA degeri.</summary>
    public decimal Value { get; }

    /// <summary>Son iki EMA arasindaki fark (slope).</summary>
    public decimal Slope { get; }

    /// <summary>EMA yukari yonlu mu?</summary>
    public bool IsRising => Slope > 0;

    public EmaResult(decimal value, decimal slope)
    {
        Value = value;
        Slope = slope;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Slope;
    }
}
