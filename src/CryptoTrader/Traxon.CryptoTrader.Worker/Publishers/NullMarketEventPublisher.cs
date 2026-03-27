using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.DTOs;

namespace Traxon.CryptoTrader.Worker.Publishers;

/// <summary>Headless deployment icin no-op publisher. Hicbir event iletmez.</summary>
public sealed class NullMarketEventPublisher : IMarketEventPublisher
{
    public void PublishCandleUpdate(CandleDto candle) { }
    public void PublishTickerUpdate(TickerDto ticker) { }
    public void PublishSignalGenerated(SignalDto signal) { }
    public void PublishTradeOpened(TradeDto trade) { }
    public void PublishTradeClosed(TradeDto trade) { }
    public void PublishPortfolioUpdate(PortfolioDto portfolio) { }
    public void PublishSystemStatus(SystemStatusDto status) { }
}
