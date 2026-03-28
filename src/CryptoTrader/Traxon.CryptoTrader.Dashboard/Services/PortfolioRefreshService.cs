using Microsoft.EntityFrameworkCore;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.DTOs;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>Her 30 saniyede DB'den en son PortfolioSnapshot'lari okuyarak Feed'e yayar.</summary>
public sealed class PortfolioRefreshService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMarketEventPublisher _publisher;
    private readonly ILogger<PortfolioRefreshService> _logger;

    public PortfolioRefreshService(
        IDbContextFactory<AppDbContext> dbFactory,
        IMarketEventPublisher publisher,
        ILogger<PortfolioRefreshService> logger)
    {
        _dbFactory = dbFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);

                // Her engine icin en son snapshot'i al
                var latestSnapshots = await db.PortfolioSnapshots
                    .GroupBy(p => p.Engine)
                    .Select(g => g.OrderByDescending(p => p.Timestamp).First())
                    .ToListAsync(stoppingToken);

                foreach (var snap in latestSnapshots)
                {
                    var winCount  = snap.WinRate.HasValue ? (int)(snap.WinRate.Value * snap.TradeCount) : 0;
                    var lossCount = snap.TradeCount - winCount;
                    var dto = new PortfolioDto(
                        Engine:            snap.Engine,
                        Balance:           snap.Balance,
                        InitialBalance:    snap.Balance - snap.TotalPnL,
                        TotalPnL:          snap.TotalPnL,
                        WinCount:          winCount,
                        LossCount:         lossCount,
                        TotalTrades:       snap.TradeCount,
                        WinRate:           snap.WinRate ?? 0m,
                        TotalExposure:     snap.TotalExposure,
                        OpenPositionCount: snap.OpenPositionCount);

                    _publisher.PublishPortfolioUpdate(dto);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PortfolioRefreshService: snapshot okuma hatasi");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
