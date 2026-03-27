using System.Collections.Concurrent;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Buffers;

/// <summary>
/// Thread-safe in-memory candle buffer backed by a fixed-capacity LinkedList per symbol/timeframe key.
/// </summary>
public sealed class InMemoryCandleBuffer : ICandleBuffer
{
    private readonly ConcurrentDictionary<string, LinkedList<Candle>> _buffers = new();
    private readonly object _lock = new();

    /// <summary>Maximum number of candles stored per key.</summary>
    public int Capacity { get; }

    public InMemoryCandleBuffer(int capacity = 200) => Capacity = capacity;

    private static string Key(Asset asset, TimeFrame timeFrame) =>
        $"{asset.Symbol}:{timeFrame.Value}";

    /// <inheritdoc/>
    public void Add(Candle candle)
    {
        var key  = Key(candle.Asset, candle.TimeFrame);
        var list = _buffers.GetOrAdd(key, _ => new LinkedList<Candle>());

        lock (_lock)
        {
            var existing = list.FirstOrDefault(c => c.OpenTime == candle.OpenTime);
            if (existing is not null)
                list.Remove(existing);

            list.AddLast(candle);

            while (list.Count > Capacity)
                list.RemoveFirst();
        }
    }

    /// <inheritdoc/>
    public Result<IReadOnlyList<Candle>> GetLast(Asset asset, TimeFrame timeFrame, int count)
    {
        var key = Key(asset, timeFrame);
        if (!_buffers.TryGetValue(key, out var list))
            return Result<IReadOnlyList<Candle>>.Failure(Error.NotEnoughCandles);

        lock (_lock)
        {
            if (list.Count < count)
                return Result<IReadOnlyList<Candle>>.Failure(Error.NotEnoughCandles);

            var result = list.TakeLast(count).ToList().AsReadOnly();
            return Result<IReadOnlyList<Candle>>.Success(result);
        }
    }

    /// <inheritdoc/>
    public Result<IReadOnlyList<Candle>> GetAll(Asset asset, TimeFrame timeFrame)
    {
        var key = Key(asset, timeFrame);
        if (!_buffers.TryGetValue(key, out var list))
            return Result<IReadOnlyList<Candle>>.Failure(Error.NotEnoughCandles);

        lock (_lock)
        {
            if (list.Count == 0)
                return Result<IReadOnlyList<Candle>>.Failure(Error.NotEnoughCandles);

            return Result<IReadOnlyList<Candle>>.Success(list.ToList().AsReadOnly());
        }
    }

    /// <inheritdoc/>
    public int Count(Asset asset, TimeFrame timeFrame)
    {
        var key = Key(asset, timeFrame);
        if (!_buffers.TryGetValue(key, out var list))
            return 0;

        lock (_lock)
        {
            return list.Count;
        }
    }

    /// <inheritdoc/>
    public bool IsWarmedUp(Asset asset, TimeFrame timeFrame, int minimumCandles = 50) =>
        Count(asset, timeFrame) >= minimumCandles;
}
