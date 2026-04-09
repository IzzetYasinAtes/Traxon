namespace Traxon.CryptoTrader.Domain.Market;

public sealed class FuturesSnapshot
{
    public long Id { get; set; }
    public string Symbol { get; set; } = null!;
    public decimal FundingRate { get; set; }
    public decimal OpenInterest { get; set; }
    public decimal OrderBookImbalance { get; set; }
    public decimal ObiPersistence { get; set; }
    public decimal BidVolume { get; set; }
    public decimal AskVolume { get; set; }
    public DateTime Timestamp { get; set; }
}
