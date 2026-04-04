using Microsoft.Data.SqlClient;
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

            // Atomic UPSERT via MERGE — explicit SqlParameter to preserve decimal precision (8 dp)
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"MERGE INTO Candles WITH (HOLDLOCK) AS target
                  USING (SELECT @p0 AS Id) AS source ON target.Id = source.Id
                  WHEN NOT MATCHED THEN
                      INSERT (Id, Symbol, [Interval], OpenTime, CloseTime,
                          [Open], High, Low, [Close], Volume, QuoteVolume, TradeCount, IsClosed)
                      VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12)
                  WHEN MATCHED THEN
                      UPDATE SET [Open] = @p5, High = @p6, Low = @p7, [Close] = @p8,
                          Volume = @p9, QuoteVolume = @p10, TradeCount = @p11, IsClosed = @p12;";
            cmd.Parameters.Add(new SqlParameter("@p0", candle.Id));
            cmd.Parameters.Add(new SqlParameter("@p1", candle.Asset.Symbol));
            cmd.Parameters.Add(new SqlParameter("@p2", candle.TimeFrame.Value));
            cmd.Parameters.Add(new SqlParameter("@p3", candle.OpenTime));
            cmd.Parameters.Add(new SqlParameter("@p4", candle.CloseTime));
            cmd.Parameters.Add(new SqlParameter("@p5", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 8, Value = candle.Open });
            cmd.Parameters.Add(new SqlParameter("@p6", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 8, Value = candle.High });
            cmd.Parameters.Add(new SqlParameter("@p7", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 8, Value = candle.Low });
            cmd.Parameters.Add(new SqlParameter("@p8", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 8, Value = candle.Close });
            cmd.Parameters.Add(new SqlParameter("@p9", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 8, Value = candle.Volume });
            cmd.Parameters.Add(new SqlParameter("@p10", System.Data.SqlDbType.Decimal) { Precision = 18, Scale = 8, Value = candle.QuoteVolume });
            cmd.Parameters.Add(new SqlParameter("@p11", candle.TradeCount));
            cmd.Parameters.Add(new SqlParameter("@p12", candle.IsClosed));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            // Log but never throw — candle persistence must not block signal generation
            _logger.LogWarning(ex,
                "Failed to write candle to DB: {Symbol}/{Interval} {OpenTime}",
                candle.Asset.Symbol, candle.TimeFrame.Value, candle.OpenTime);
        }
    }
}
