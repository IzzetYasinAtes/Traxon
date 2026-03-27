using System.Collections.Concurrent;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.DTOs;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>Singleton event bus. Hem ILiveFeedService hem IMarketEventPublisher implement eder.</summary>
public sealed class LiveFeedService : ILiveFeedService, IMarketEventPublisher
{
    private readonly ConcurrentDictionary<string, TickerDto> _tickers = new();
    private readonly List<SignalDto> _signals = [];
    private readonly List<TradeDto> _trades = [];
    private readonly ConcurrentDictionary<string, PortfolioDto> _portfolios = new();
    private readonly ConcurrentDictionary<(string, string), CandleDto> _candles = new();
    private readonly object _signalLock = new();
    private readonly object _tradeLock = new();
    private SystemStatusDto? _systemStatus;

    // ILiveFeedService
    public IReadOnlyDictionary<string, TickerDto> Tickers => _tickers;

    public IReadOnlyList<SignalDto> RecentSignals
    {
        get { lock (_signalLock) return _signals.TakeLast(20).ToList(); }
    }

    public IReadOnlyList<TradeDto> RecentTrades
    {
        get { lock (_tradeLock) return _trades.TakeLast(50).ToList(); }
    }

    public IReadOnlyDictionary<string, PortfolioDto> Portfolios => _portfolios;
    public IReadOnlyDictionary<(string Symbol, string Interval), CandleDto> LatestCandles => _candles;
    public SystemStatusDto? SystemStatus => _systemStatus;

    public event Action<TickerDto>? OnTickerUpdate;
    public event Action<SignalDto>? OnSignalGenerated;
    public event Action<TradeDto>? OnTradeOpened;
    public event Action<TradeDto>? OnTradeClosed;
    public event Action<PortfolioDto>? OnPortfolioUpdate;
    public event Action<CandleDto>? OnCandleUpdate;
    public event Action<SystemStatusDto>? OnSystemStatusUpdate;

    // IMarketEventPublisher
    public void PublishTickerUpdate(TickerDto ticker)
    {
        _tickers[ticker.Symbol] = ticker;
        var handlers = OnTickerUpdate;
        handlers?.Invoke(ticker);
    }

    public void PublishCandleUpdate(CandleDto candle)
    {
        _candles[(candle.Symbol, candle.Interval)] = candle;
        var handlers = OnCandleUpdate;
        handlers?.Invoke(candle);
    }

    public void PublishSignalGenerated(SignalDto signal)
    {
        lock (_signalLock)
        {
            _signals.Add(signal);
            if (_signals.Count > 100) _signals.RemoveAt(0);
        }
        var handlers = OnSignalGenerated;
        handlers?.Invoke(signal);
    }

    public void PublishTradeOpened(TradeDto trade)
    {
        lock (_tradeLock)
        {
            _trades.Add(trade);
            if (_trades.Count > 200) _trades.RemoveAt(0);
        }
        var handlers = OnTradeOpened;
        handlers?.Invoke(trade);
    }

    public void PublishTradeClosed(TradeDto trade)
    {
        lock (_tradeLock)
        {
            var idx = _trades.FindIndex(t => t.TradeId == trade.TradeId);
            if (idx >= 0) _trades[idx] = trade;
        }
        var handlers = OnTradeClosed;
        handlers?.Invoke(trade);
    }

    public void PublishPortfolioUpdate(PortfolioDto portfolio)
    {
        _portfolios[portfolio.Engine] = portfolio;
        var handlers = OnPortfolioUpdate;
        handlers?.Invoke(portfolio);
    }

    public void PublishSystemStatus(SystemStatusDto status)
    {
        _systemStatus = status;
        var handlers = OnSystemStatusUpdate;
        handlers?.Invoke(status);
    }
}
