namespace Traxon.CryptoTrader.Application.DTOs;

public record CandleDto(
    string Symbol,
    string Interval,
    DateTime OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    bool IsClosed);
