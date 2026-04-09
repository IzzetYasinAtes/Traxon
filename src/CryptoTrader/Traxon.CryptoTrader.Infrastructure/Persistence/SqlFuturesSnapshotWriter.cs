using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Persistence;

public sealed class SqlFuturesSnapshotWriter : IFuturesSnapshotWriter
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<SqlFuturesSnapshotWriter> _logger;

    public SqlFuturesSnapshotWriter(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<SqlFuturesSnapshotWriter> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task WriteAsync(IReadOnlyList<FuturesSnapshot> snapshots, CancellationToken ct)
    {
        if (snapshots.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.FuturesSnapshots.AddRange(snapshots);
        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Saved {Count} futures snapshots to DB", snapshots.Count);
    }
}
