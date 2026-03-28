namespace Traxon.CryptoTrader.Application.Polymarket.Models;

public sealed record PolymarketOrderBook
{
    public string                        TokenId { get; init; } = string.Empty;
    public IReadOnlyList<PolymarketLevel> Bids   { get; init; } = [];
    public IReadOnlyList<PolymarketLevel> Asks   { get; init; } = [];

    public decimal BestBid  => Bids.Count > 0 ? Bids.Max(b => b.Price) : 0m;
    public decimal BestAsk  => Asks.Count > 0 ? Asks.Min(a => a.Price) : 1m;
    public decimal Midpoint => (BestBid + BestAsk) / 2m;
}

public sealed record PolymarketLevel(decimal Price, decimal Size);
