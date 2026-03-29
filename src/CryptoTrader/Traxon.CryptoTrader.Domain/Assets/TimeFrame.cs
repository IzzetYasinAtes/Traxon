using Traxon.CryptoTrader.Domain.Abstractions;

namespace Traxon.CryptoTrader.Domain.Assets;

public sealed class TimeFrame : ValueObject
{
    public string Value { get; }
    public int TotalSeconds { get; }
    public TimeSpan Duration => TimeSpan.FromSeconds(TotalSeconds);

    // EF Core parametresiz constructor (owned entity materialization icin)
    private TimeFrame() { Value = null!; }

    private TimeFrame(string value, int totalSeconds)
    {
        Value = value;
        TotalSeconds = totalSeconds;
    }

    public static readonly TimeFrame OneMinute     = new("1m",  60);
    public static readonly TimeFrame FiveMinute    = new("5m",  300);
    public static readonly TimeFrame FifteenMinute = new("15m", 900);
    public static readonly TimeFrame OneHour       = new("1h",  3600);

    public static readonly IReadOnlyList<TimeFrame> All = [OneMinute, FiveMinute, FifteenMinute, OneHour];

    /// <summary>Sinyal uretimi icin kullanilan timeframe'ler (5m, 15m).</summary>
    public static readonly IReadOnlyList<TimeFrame> SignalTimeFrames = [FiveMinute, FifteenMinute];

    /// <summary>Trend dogrulama icin kullanilan timeframe (1h).</summary>
    public static readonly TimeFrame TrendTimeFrame = OneHour;

    public static TimeFrame? FromValue(string value) =>
        All.FirstOrDefault(t => t.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    public override string ToString() => Value;
}
