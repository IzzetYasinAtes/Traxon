using Traxon.CryptoTrader.Domain.Abstractions;

namespace Traxon.CryptoTrader.Domain.Assets;

public sealed class Asset : ValueObject
{
    public string Symbol { get; }
    public string BaseAsset { get; }
    public string QuoteAsset { get; }

    // EF Core parametresiz constructor (owned entity materialization icin)
    private Asset() { Symbol = null!; BaseAsset = null!; QuoteAsset = null!; }

    private Asset(string symbol, string baseAsset, string quoteAsset)
    {
        Symbol = symbol;
        BaseAsset = baseAsset;
        QuoteAsset = quoteAsset;
    }

    public static readonly Asset BTCUSDT  = new("BTCUSDT",  "BTC",  "USDT");
    public static readonly Asset ETHUSDT  = new("ETHUSDT",  "ETH",  "USDT");
    public static readonly Asset SOLUSDT  = new("SOLUSDT",  "SOL",  "USDT");
    public static readonly Asset XRPUSDT  = new("XRPUSDT",  "XRP",  "USDT");
    public static readonly Asset DOGEUSDT = new("DOGEUSDT", "DOGE", "USDT");
    public static readonly Asset AVAXUSDT = new("AVAXUSDT", "HYPE", "USDT");
    public static readonly Asset BNBUSDT  = new("BNBUSDT",  "BNB",  "USDT");

    public static readonly IReadOnlyList<Asset> All = [BTCUSDT, ETHUSDT, SOLUSDT, XRPUSDT, DOGEUSDT, AVAXUSDT, BNBUSDT];

    public static Asset? FromSymbol(string symbol) =>
        All.FirstOrDefault(a => a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Symbol; }

    public override string ToString() => Symbol;
}
