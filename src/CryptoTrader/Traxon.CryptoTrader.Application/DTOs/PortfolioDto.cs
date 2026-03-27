namespace Traxon.CryptoTrader.Application.DTOs;

public record PortfolioDto(
    string Engine,
    decimal Balance,
    decimal InitialBalance,
    decimal TotalPnL,
    int WinCount,
    int LossCount,
    int TotalTrades,
    decimal WinRate,
    decimal TotalExposure,
    int OpenPositionCount);
