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
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (IsDuplicateKeyException(ex))
        {
            // Race condition: same candle inserted concurrently — expected, skip silently
            _logger.LogDebug("Candle already exists (concurrent insert), skipping: {Symbol}/{Interval} {OpenTime}",
                candle.Asset.Symbol, candle.TimeFrame.Value, candle.OpenTime);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to write candle to DB: {Symbol}/{Interval} {OpenTime}",
                candle.Asset.Symbol, candle.TimeFrame.Value, candle.OpenTime);
        }
    }

    private static bool IsDuplicateKeyException(Microsoft.EntityFrameworkCore.DbUpdateException ex)
        => ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
