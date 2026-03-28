using Microsoft.EntityFrameworkCore;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Mappings;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>
/// Dashboard baslarken DB'deki son 50 trade'i LiveFeedService'e yukler.
/// Worker process'i ayri calistigi icin Dashboard'un in-memory LiveFeed'i bos baslar —
/// bu service baslangicta onceki trade'leri geri yukleyerek UI'i doldurmak icin kullanilir.
/// </summary>
public sealed class LiveFeedSeederService : IHostedService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMarketEventPublisher           _publisher;
    private readonly ILogger<LiveFeedSeederService>  _logger;

    public LiveFeedSeederService(
        IDbContextFactory<AppDbContext> dbFactory,
        IMarketEventPublisher publisher,
        ILogger<LiveFeedSeederService> logger)
    {
        _dbFactory = dbFactory;
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var recentTrades = await db.Trades
                .OrderByDescending(t => t.OpenedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            // Kronolojik siraya cevir (en eski once) — LiveFeed'deki siralamayla uyumlu olsun
            recentTrades.Reverse();

            foreach (var trade in recentTrades)
                _publisher.PublishTradeOpened(trade.ToDto());

            _logger.LogInformation(
                "LiveFeedSeeder: {Count} trade DB'den yuklenip LiveFeedService'e eklendi.",
                recentTrades.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LiveFeedSeeder: trade seed islemi basarisiz — dashboard bos baslayacak.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
