using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Mappings;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>Her 5 saniyede engine'lerden portfolio ve acik trade verisi ceker.</summary>
public sealed class PortfolioRefreshService : BackgroundService
{
    private readonly IEnumerable<ITradingEngine> _engines;
    private readonly IMarketEventPublisher _publisher;
    private readonly ILogger<PortfolioRefreshService> _logger;

    public PortfolioRefreshService(
        IEnumerable<ITradingEngine> engines,
        IMarketEventPublisher publisher,
        ILogger<PortfolioRefreshService> logger)
    {
        _engines = engines;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var engine in _engines)
            {
                var portfolioResult = await engine.GetPortfolioAsync(stoppingToken);
                if (portfolioResult.IsSuccess && portfolioResult.Value is not null)
                    _publisher.PublishPortfolioUpdate(portfolioResult.Value.ToDto());

                var tradesResult = await engine.GetOpenTradesAsync(stoppingToken);
                if (tradesResult.IsSuccess && tradesResult.Value is not null)
                    foreach (var trade in tradesResult.Value)
                        _publisher.PublishTradeOpened(trade.ToDto());
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
