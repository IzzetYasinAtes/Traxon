namespace Traxon.CryptoTrader.Admin.Services;

public sealed class ConfigService
{
    private readonly object _lock = new();
    private TradingConfig _config = new();

    public TradingConfig GetConfig() { lock (_lock) return _config; }

    public void Apply(TradingConfig config)
    {
        lock (_lock) _config = config;
        OnConfigChanged?.Invoke(config);
    }

    public event Action<TradingConfig>? OnConfigChanged;
}

public record TradingConfig
{
    public decimal MinEdge { get; init; } = 0.05m;
    public decimal KellyFraction { get; init; } = 0.25m;
    public decimal MaxPositionSize { get; init; } = 50m;
    public decimal Bankroll { get; init; } = 1000m;
    public bool PaperPolyEnabled { get; init; } = true;
    public bool PaperBinanceEnabled { get; init; } = true;
    public int MaxOpenPositions { get; init; } = 3;
    public decimal MinConfidence { get; init; } = 0.55m;
}
