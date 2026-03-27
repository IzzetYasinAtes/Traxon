namespace Traxon.CryptoTrader.Application.DTOs;

public record TickerDto(
    string Symbol,
    decimal Price,
    decimal Change24h,
    decimal ChangePercent24h,
    DateTime UpdatedAt);
