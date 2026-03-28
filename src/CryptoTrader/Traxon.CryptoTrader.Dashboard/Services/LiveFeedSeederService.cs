using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.DTOs;
using Traxon.CryptoTrader.Application.Mappings;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>
/// Dashboard baslarken DB'deki son kayitlari LiveFeedService'e yukler.
/// Worker process'i ayri calistigi icin Dashboard'un in-memory LiveFeed'i bos baslar —
/// bu service baslangicta onceki trade ve signal'lari geri yukleyerek UI'i doldurmak icin kullanilir.
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

            // Once signal'lari seed et — SignalCard bileşenlerinin görünmesi için
            var signalTrades = recentTrades.TakeLast(20).ToList();
            foreach (var trade in signalTrades)
            {
                var signalDto = ReconstructSignalFromTrade(trade);
                _publisher.PublishSignalGenerated(signalDto);
            }

            // Sonra trade'leri seed et
            foreach (var trade in recentTrades)
                _publisher.PublishTradeOpened(trade.ToDto());

            _logger.LogInformation(
                "LiveFeedSeeder: {TradeCount} trade ve {SignalCount} signal DB'den yuklenip LiveFeedService'e eklendi.",
                recentTrades.Count, signalTrades.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LiveFeedSeeder: seed islemi basarisiz — dashboard bos baslayacak.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// DB'deki bir trade'den SignalDto yeniden olusturur.
    /// Trade'in IndicatorSnapshot JSON'i RSI, MACD ve bullish count iceriyor.
    /// </summary>
    private static SignalDto ReconstructSignalFromTrade(Trade trade)
    {
        decimal rsi      = 0m;
        decimal macdHist = 0m;
        int     bullish  = 0;

        try
        {
            using var doc  = JsonDocument.Parse(trade.IndicatorSnapshot);
            var       root = doc.RootElement;

            if (root.TryGetProperty("rsi", out var rsiProp))
                rsi = rsiProp.GetDecimal();

            if (root.TryGetProperty("macd_hist", out var macdProp))
                macdHist = macdProp.GetDecimal();

            if (root.TryGetProperty("bullish", out var bullProp))
                bullish = bullProp.GetInt32();
        }
        catch
        {
            // IndicatorSnapshot parse edilemedi — varsayilan degerler kullanilir
        }

        return new SignalDto(
            SignalId:      trade.Id,
            Symbol:        trade.Asset.Symbol,
            Interval:      trade.TimeFrame.Value,
            Direction:     trade.Direction == SignalDirection.Up ? "UP" : "DOWN",
            FairValue:     trade.FairValue,
            MarketPrice:   trade.EntryPrice,   // gercek market fiyati yok, entry yakin tahmin
            Edge:          trade.Edge,
            KellyFraction: trade.KellyFraction,
            Regime:        trade.Regime == MarketRegime.HighVolatility ? "HIGH_VOL" : "LOW_VOL",
            Rsi:           rsi,
            MacdHistogram: macdHist,
            BullishCount:  bullish,
            GeneratedAt:   trade.OpenedAt);
    }
}
