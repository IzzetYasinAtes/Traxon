namespace Traxon.CryptoTrader.Application.DTOs;

public record SystemStatusDto(
    bool IsRunning,
    bool IsBinanceConnected,
    int ActiveEngineCount,
    DateTime StartedAt);
