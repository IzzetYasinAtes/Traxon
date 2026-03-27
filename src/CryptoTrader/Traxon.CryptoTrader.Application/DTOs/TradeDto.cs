namespace Traxon.CryptoTrader.Application.DTOs;

public record TradeDto(
    Guid TradeId,
    string Engine,
    string Symbol,
    string Interval,
    string Direction,         // "UP" | "DOWN"
    decimal EntryPrice,
    decimal? ExitPrice,
    decimal FairValue,
    decimal Edge,
    decimal PositionSize,
    string Status,            // "OPEN" | "CLOSED"
    string? Outcome,          // "WIN" | "LOSS" | null
    decimal? PnL,
    DateTime OpenedAt,
    DateTime? ClosedAt);
