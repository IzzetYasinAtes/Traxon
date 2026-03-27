using Traxon.CryptoTrader.Application.DTOs;

namespace Traxon.CryptoTrader.Dashboard.Hubs;

public interface ICryptoHubClient
{
    Task ReceiveTickerUpdate(TickerDto ticker);
    Task ReceiveSignalGenerated(SignalDto signal);
    Task ReceiveTradeOpened(TradeDto trade);
    Task ReceiveTradeClosed(TradeDto trade);
    Task ReceivePortfolioUpdate(PortfolioDto portfolio);
    Task ReceiveCandleUpdate(CandleDto candle);
    Task ReceiveSystemStatus(SystemStatusDto status);
}
