using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Persistence;

public sealed class SqlCandleWriter : ICandleWriter
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<SqlCandleWriter>        _logger;

    public SqlCandleWriter(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<SqlCandleWriter> logger)
    {
        _dbFactory = dbFactory;
        _logger    = logger;
    }

    public async Task WriteAsync(Candle candle, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var exists = await db.Candles.AnyAsync(c => c.Id == candle.Id, ct);

            if (!exists)
            {
                db.Candles.Add(candle);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to write candle to DB: {Symbol}/{Interval} {OpenTime}",
                candle.Asset.Symbol, candle.TimeFrame.Value, candle.OpenTime);
        }
    }
}
