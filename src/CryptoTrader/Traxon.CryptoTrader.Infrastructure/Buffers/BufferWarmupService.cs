using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Infrastructure.Buffers;

/// <summary>
/// Startup'ta DB'deki mevcut 1m mumları InMemoryCandleBuffer'a yükler.
/// Restart sonrası sinyal üretimi için bekleme süresini ortadan kaldırır.
/// </summary>
public sealed class BufferWarmupService : IHostedService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ICandleBuffer _candleBuffer;
    private readonly ILogger<BufferWarmupService> _logger;

    public BufferWarmupService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ICandleBuffer candleBuffer,
        ILogger<BufferWarmupService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _candleBuffer = candleBuffer;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BufferWarmupService starting — loading 1m candles from DB...");

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var totalLoaded = 0;

            // Only load 1m candles — no higher timeframes needed
            foreach (var asset in Asset.Tradeable)
            {
                var fetchCount = 4500; // 3-day backfill capacity

                var candles = await dbContext.Candles
                    .Where(c => c.IsClosed
                        && c.Asset.Symbol == asset.Symbol
                        && c.TimeFrame.Value == "1m")
                    .OrderByDescending(c => c.OpenTime)
                    .Take(fetchCount)
                    .ToListAsync(cancellationToken);

                // Eski -> yeni sirasyla buffer'a ekle
                foreach (var candle in candles.OrderBy(c => c.OpenTime))
                {
                    _candleBuffer.Add(candle);
                }

                totalLoaded += candles.Count;
                _logger.LogInformation(
                    "BufferWarmup: {Symbol}/1m — {Count} candle loaded",
                    asset.Symbol, candles.Count);
            }

            _logger.LogInformation(
                "BufferWarmupService completed — {Total} 1m candles loaded",
                totalLoaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BufferWarmupService failed to load candles from DB");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
