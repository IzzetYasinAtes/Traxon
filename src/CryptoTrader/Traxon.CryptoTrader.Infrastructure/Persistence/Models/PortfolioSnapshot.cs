namespace Traxon.CryptoTrader.Infrastructure.Persistence.Models;

/// <summary>Infrastructure persistence modeli — Domain entity degil.</summary>
public sealed class PortfolioSnapshot
{
    public long     Id                 { get; set; }
    public string   Engine             { get; set; } = string.Empty;
    public DateTime Timestamp          { get; set; }
    public decimal  Balance            { get; set; }
    public int      OpenPositionCount  { get; set; }
    public decimal  TotalExposure      { get; set; }
    public decimal  TotalPnL           { get; set; }
    public decimal? WinRate            { get; set; }
    public int      TradeCount         { get; set; }
}
