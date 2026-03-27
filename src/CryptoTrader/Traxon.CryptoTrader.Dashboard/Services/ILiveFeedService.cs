using Traxon.CryptoTrader.Application.DTOs;

namespace Traxon.CryptoTrader.Dashboard.Services;

public interface ILiveFeedService
{
    // Anlik durum (snapshot)
    IReadOnlyDictionary<string, TickerDto> Tickers { get; }
    IReadOnlyList<SignalDto> RecentSignals { get; }
    IReadOnlyList<TradeDto> RecentTrades { get; }
    IReadOnlyDictionary<string, PortfolioDto> Portfolios { get; }
    IReadOnlyDictionary<(string Symbol, string Interval), CandleDto> LatestCandles { get; }
    SystemStatusDto? SystemStatus { get; }

    // C# event'leri (Blazor components subscribe eder)
    event Action<TickerDto>? OnTickerUpdate;
    event Action<SignalDto>? OnSignalGenerated;
    event Action<TradeDto>? OnTradeOpened;
    event Action<TradeDto>? OnTradeClosed;
    event Action<PortfolioDto>? OnPortfolioUpdate;
    event Action<CandleDto>? OnCandleUpdate;
    event Action<SystemStatusDto>? OnSystemStatusUpdate;
}
