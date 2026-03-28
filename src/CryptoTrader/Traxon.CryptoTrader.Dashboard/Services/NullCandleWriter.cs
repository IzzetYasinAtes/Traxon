using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Dashboard.Services;

/// <summary>Dashboard icin no-op candle writer. Persistans Worker'a birakilir.</summary>
internal sealed class NullCandleWriter : ICandleWriter
{
    public Task WriteAsync(Candle candle, CancellationToken ct = default) => Task.CompletedTask;
}
