namespace Traxon.CryptoTrader.Application.Polymarket.Models;

public sealed record PolymarketMarket
{
    public string ConditionId       { get; init; } = string.Empty;
    public string Question          { get; init; } = string.Empty;
    public string YesTokenId        { get; init; } = string.Empty;
    public string NoTokenId         { get; init; } = string.Empty;
    public long   EndDateUtcSeconds { get; init; }
    public bool   Active            { get; init; }
    public bool   Closed            { get; init; }
    public string UnderlyingAsset   { get; init; } = string.Empty;
    public string Direction         { get; init; } = string.Empty;

    /// <summary>Resolved price for this direction's token (0.0 or 1.0 when resolved, null when market is open)</summary>
    public decimal? ResolvedPrice   { get; init; }

    public string        RelevantTokenId => Direction == "Up" ? YesTokenId : NoTokenId;
    public DateTimeOffset EndDate        => DateTimeOffset.FromUnixTimeSeconds(EndDateUtcSeconds);
    public TimeSpan       TimeLeft       => EndDate - DateTimeOffset.UtcNow;
}
