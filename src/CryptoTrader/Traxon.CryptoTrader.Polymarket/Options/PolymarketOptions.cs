namespace Traxon.CryptoTrader.Polymarket.Options;

public sealed class PolymarketOptions
{
    public const string SectionName = "Polymarket";

    /// <summary>false = engine tamamen pasif, order gönderilmez.</summary>
    public bool    Enabled                   { get; set; } = false;
    public string  ApiKey                    { get; set; } = string.Empty;
    public string  ApiSecret                 { get; set; } = string.Empty;
    public string  Passphrase                { get; set; } = string.Empty;
    public string  WalletAddress             { get; set; } = string.Empty;
    public string  BaseUrl                   { get; set; } = "https://clob.polymarket.com";
    public string  WebSocketUrl              { get; set; } = "wss://ws-subscriptions-clob.polymarket.com/ws/market";
    public string  GammaApiUrl               { get; set; } = "https://gamma-api.polymarket.com";
    public int     HeartbeatIntervalSeconds  { get; set; } = 5;
    public int     WsPingIntervalSeconds     { get; set; } = 10;
    public decimal MaxPositionSizeUsdc       { get; set; } = 10m;
    public int     MarketMinutesMinRemaining { get; set; } = 10;
}
