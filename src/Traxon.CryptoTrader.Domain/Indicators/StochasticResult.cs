namespace Traxon.CryptoTrader.Domain.Indicators;

public sealed class StochasticResult : IndicatorResult
{
    public decimal K { get; }
    public decimal D { get; }
    public bool IsKAboveD => K > D;
    public bool IsOverbought => K >= 80;
    public bool IsOversold   => K <= 20;

    public StochasticResult(decimal k, decimal d) { K = k; D = d; }

    protected override IEnumerable<object?> GetEqualityComponents()
    { yield return K; yield return D; }
}
