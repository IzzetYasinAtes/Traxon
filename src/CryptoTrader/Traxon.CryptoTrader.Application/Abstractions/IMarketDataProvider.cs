using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IMarketDataProvider
{
    /// <summary>
    /// REST ile geçmiş candle'ları çek. startTime verilirse o zamandan itibaren çeker.
    /// </summary>
    Task<Result<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        Asset asset,
        TimeFrame timeFrame,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// REST ile belirli bir zaman aralığından itibaren candle çeker (paginated backfill).
    /// </summary>
    Task<Result<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        Asset asset,
        TimeFrame timeFrame,
        int limit,
        DateTime startTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// WebSocket stream'i başlat. Her kapanan candle'da callback çağrılır.
    /// </summary>
    Task StartStreamAsync(
        IReadOnlyList<Asset> assets,
        IReadOnlyList<TimeFrame> timeFrames,
        Func<Candle, Task> onCandleClosed,
        CancellationToken cancellationToken = default);

    Task StopStreamAsync(CancellationToken cancellationToken = default);

    bool IsConnected { get; }
}
