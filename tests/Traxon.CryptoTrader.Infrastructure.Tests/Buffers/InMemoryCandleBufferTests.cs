using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Infrastructure.Buffers;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;

namespace Traxon.CryptoTrader.Infrastructure.Tests.Buffers;

public class InMemoryCandleBufferTests
{
    private static Candle MakeCandle(Asset asset, TimeFrame tf, int minuteOffset = 0) =>
        new(
            id: DateTime.UtcNow.AddMinutes(minuteOffset).Ticks,
            asset: asset,
            timeFrame: tf,
            openTime: DateTime.UtcNow.AddMinutes(minuteOffset),
            closeTime: DateTime.UtcNow.AddMinutes(minuteOffset + 5),
            open: 100m, high: 101m, low: 99m, close: 100.5m,
            volume: 1000m, quoteVolume: 100500m, tradeCount: 500, isClosed: true);

    [Fact]
    public void Add_ShouldStore_Candle()
    {
        var buffer = new InMemoryCandleBuffer(200);
        var candle = MakeCandle(Asset.BTCUSDT, TimeFrame.FiveMinute);

        buffer.Add(candle);

        buffer.Count(Asset.BTCUSDT, TimeFrame.FiveMinute).Should().Be(1);
    }

    [Fact]
    public void Add_ShouldRespect_Capacity()
    {
        var buffer = new InMemoryCandleBuffer(capacity: 5);

        for (int i = 0; i < 10; i++)
            buffer.Add(MakeCandle(Asset.BTCUSDT, TimeFrame.FiveMinute, minuteOffset: i));

        buffer.Count(Asset.BTCUSDT, TimeFrame.FiveMinute).Should().Be(5);
    }

    [Fact]
    public void GetLast_ShouldReturn_Failure_WhenNotEnough()
    {
        var buffer = new InMemoryCandleBuffer(200);
        buffer.Add(MakeCandle(Asset.BTCUSDT, TimeFrame.FiveMinute));

        var result = buffer.GetLast(Asset.BTCUSDT, TimeFrame.FiveMinute, count: 10);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void IsWarmedUp_ShouldReturnTrue_WhenSufficientCandles()
    {
        var buffer = new InMemoryCandleBuffer(200);
        for (int i = 0; i < 55; i++)
            buffer.Add(MakeCandle(Asset.BTCUSDT, TimeFrame.FiveMinute, minuteOffset: i));

        buffer.IsWarmedUp(Asset.BTCUSDT, TimeFrame.FiveMinute, minimumCandles: 50)
              .Should().BeTrue();
    }
}
