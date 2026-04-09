using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IFuturesSnapshotWriter
{
    Task WriteAsync(IReadOnlyList<FuturesSnapshot> snapshots, CancellationToken ct);
}
