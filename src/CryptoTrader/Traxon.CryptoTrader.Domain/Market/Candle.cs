using Traxon.CryptoTrader.Domain.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Domain.Market;

public sealed class Candle : Entity<long>
{
    public Asset Asset { get; }
    public TimeFrame TimeFrame { get; }
    public DateTime OpenTime { get; }
    public DateTime CloseTime { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public decimal Volume { get; }
    public decimal QuoteVolume { get; }
    public int TradeCount { get; }
    public bool IsClosed { get; }

    public decimal Typical => (High + Low + Close) / 3m;
    public decimal Range   => High - Low;
    public bool IsBullish  => Close >= Open;
    public bool IsBearish  => Close < Open;

    private Candle() { Asset = null!; TimeFrame = null!; }

    public Candle(
        long id,
        Asset asset,
        TimeFrame timeFrame,
        DateTime openTime,
        DateTime closeTime,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        decimal quoteVolume,
        int tradeCount,
        bool isClosed)
    {
        Id = id;
        Asset = asset;
        TimeFrame = timeFrame;
        OpenTime = openTime;
        CloseTime = closeTime;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        QuoteVolume = quoteVolume;
        TradeCount = tradeCount;
        IsClosed = isClosed;
    }
}
