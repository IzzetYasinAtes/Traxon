using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Infrastructure.Buffers;

/// <summary>
/// Startup'ta DB'deki mevcut mumları InMemoryCandleBuffer'a yükler.
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
        _logger.LogInformation("BufferWarmupService starting — loading candles from DB...");

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Her symbol/timeframe kombinasyonu için son N (buffer capacity) mumu çek
            var groups = await dbContext.Candles
                .Where(c => c.IsClosed)
                .GroupBy(c => new { Symbol = c.Asset.Symbol, TimeFrame = c.TimeFrame.Value })
                .Select(g => new { g.Key.Symbol, g.Key.TimeFrame })
                .ToListAsync(cancellationToken);

            var totalLoaded = 0;

            foreach (var group in groups)
            {
                var capacity = _candleBuffer.Capacity;

                // TimeFrame bazlı kapasiteyi kullanmak için buffer'ın GetCapacity'sini
                // dolaylı olarak kullanıyoruz — Add() zaten doğru kapasiteyi uygular.
                // Güvenli tarafta kalmak için en büyük olası kapasiteyi çekiyoruz (500).
                var fetchCount = Math.Max(capacity, 500);

                var candles = await dbContext.Candles
                    .Where(c => c.IsClosed
                        && c.Asset.Symbol == group.Symbol
                        && c.TimeFrame.Value == group.TimeFrame)
                    .OrderByDescending(c => c.OpenTime)
                    .Take(fetchCount)
                    .ToListAsync(cancellationToken);

                // Eski → yeni sırasıyla buffer'a ekle
                foreach (var candle in candles.OrderBy(c => c.OpenTime))
                {
                    _candleBuffer.Add(candle);
                }

                totalLoaded += candles.Count;
                _logger.LogInformation(
                    "BufferWarmup: {Symbol}/{TimeFrame} — {Count} candle loaded",
                    group.Symbol, group.TimeFrame, candles.Count);
            }

            _logger.LogInformation(
                "BufferWarmupService completed — {Total} candles loaded for {Groups} symbol/timeframe groups",
                totalLoaded, groups.Count);

            // Aggregate 1m candles into higher timeframes
            AggregateOneMinuteCandles();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BufferWarmupService failed to load candles from DB");
            // Startup'ı bloklamıyoruz — buffer boş kalırsa MarketDataWorker dolduracak
        }
    }

    private void AggregateOneMinuteCandles()
    {
        foreach (var asset in Asset.Tradeable)
        {
            var oneMinResult = _candleBuffer.GetAll(asset, TimeFrame.OneMinute);
            if (oneMinResult.IsFailure) continue;

            foreach (var targetTf in TimeFrame.Aggregated)
            {
                var aggregated = CandleAggregator.AggregateAll(oneMinResult.Value!, asset, targetTf);
                foreach (var candle in aggregated)
                    _candleBuffer.Add(candle);

                _logger.LogInformation(
                    "BufferWarmup aggregated {Count} {TF} candles for {Symbol}",
                    aggregated.Count, targetTf.Value, asset.Symbol);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
