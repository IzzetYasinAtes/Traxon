using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Application.Abstractions;

public interface IMarketDataProvider
{
    /// <summary>
    /// Startup'ta REST ile 200 candle çek ve buffer'ı doldur.
    /// </summary>
    Task<Result<IReadOnlyList<Candle>>> GetHistoricalCandlesAsync(
        Asset asset,
        TimeFrame timeFrame,
        int limit,
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
