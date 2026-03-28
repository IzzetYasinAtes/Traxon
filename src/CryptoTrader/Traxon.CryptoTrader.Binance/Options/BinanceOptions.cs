namespace Traxon.CryptoTrader.Binance.Options;

public sealed class BinanceOptions
{
    public const string SectionName = "Binance";

    public int KlineWeight { get; set; } = 2;
    public int HistoricalLimit { get; set; } = 200;
    public int ReconnectDelaySeconds { get; set; } = 5;

    // Real trading engine — GUVENLIK: default disabled
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public decimal MaxPositionSize { get; set; } = 100m;
    public IReadOnlyList<string> AllowedSymbols { get; set; } = ["BTCUSDT", "ETHUSDT"];
}
