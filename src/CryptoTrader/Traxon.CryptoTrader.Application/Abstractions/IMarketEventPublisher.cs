using Traxon.CryptoTrader.Application.DTOs;

namespace Traxon.CryptoTrader.Application.Abstractions;

/// <summary>Application port — Dashboard bu interface'i implement eder, Worker bu interface'i cagirr.</summary>
public interface IMarketEventPublisher
{
    void PublishCandleUpdate(CandleDto candle);
    void PublishTickerUpdate(TickerDto ticker);
    void PublishSignalGenerated(SignalDto signal);
    void PublishTradeOpened(TradeDto trade);
    void PublishTradeClosed(TradeDto trade);
    void PublishPortfolioUpdate(PortfolioDto portfolio);
    void PublishSystemStatus(SystemStatusDto status);
}
