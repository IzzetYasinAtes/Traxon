namespace Traxon.CryptoTrader.Domain.Indicators;

/// <summary>Hacim analiz sonucu — mevcut hacim, ortalama ve oran.</summary>
public sealed class VolumeResult : IndicatorResult
{
    /// <summary>Son candle'in hacmi.</summary>
    public decimal CurrentVolume { get; }

    /// <summary>Ortalama hacim (SMA).</summary>
    public decimal AverageVolume { get; }

    /// <summary>CurrentVolume / AverageVolume orani.</summary>
    public decimal Ratio => AverageVolume > 0 ? CurrentVolume / AverageVolume : 0m;

    public VolumeResult(decimal currentVolume, decimal averageVolume)
    {
        CurrentVolume = currentVolume;
        AverageVolume = averageVolume;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CurrentVolume;
        yield return AverageVolume;
    }
}
