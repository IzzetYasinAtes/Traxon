namespace Traxon.CryptoTrader.Application.DTOs;

public record SignalDto(
    Guid SignalId,
    string Symbol,
    string Interval,
    string Direction,         // "UP" | "DOWN"
    decimal FairValue,
    decimal MarketPrice,
    decimal Edge,
    decimal KellyFraction,
    string Regime,            // "LOW_VOL" | "HIGH_VOL"
    decimal Rsi,
    decimal MacdHistogram,
    int BullishCount,
    DateTime GeneratedAt);
