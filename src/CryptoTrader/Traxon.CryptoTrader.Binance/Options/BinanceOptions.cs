namespace Traxon.CryptoTrader.Binance.Options;

public sealed class BinanceOptions
{
    public const string SectionName = "Binance";

    public int KlineWeight { get; set; } = 2;
    public int HistoricalLimit { get; set; } = 200;
    public int ReconnectDelaySeconds { get; set; } = 5;
}
