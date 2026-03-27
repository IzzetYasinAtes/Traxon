using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Infrastructure.Calculators;

namespace Traxon.CryptoTrader.Binance.Tests.Calculators;

public class IndicatorCalculatorTests
{
    private readonly IndicatorCalculator _sut =
        new(NullLogger<IndicatorCalculator>.Instance);

    private static List<Candle> GenerateCandles(int count, decimal startPrice = 100m)
    {
        var candles = new List<Candle>();
        var price   = startPrice;
        var rng     = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var change = (decimal)(rng.NextDouble() - 0.48) * 2;
            price = Math.Max(price + change, 0.01m);
            var high = price + (decimal)(rng.NextDouble() * 0.5);
            var low  = Math.Max(price - (decimal)(rng.NextDouble() * 0.5), 0.01m);

            candles.Add(new Candle(
                id: i,
                asset: Asset.BTCUSDT,
                timeFrame: TimeFrame.FiveMinute,
                openTime: DateTime.UtcNow.AddMinutes(-count + i),
                closeTime: DateTime.UtcNow.AddMinutes(-count + i + 5),
                open: price, high: high, low: low, close: price,
                volume: 1000m + i, quoteVolume: (1000m + i) * price,
                tradeCount: 100, isClosed: true));
        }
        return candles;
    }

    [Fact]
    public void CalculateRsi_ShouldReturn_ValueBetween0And100()
    {
        var closes = GenerateCandles(50).Select(c => c.Close).ToList();
        var result = _sut.CalculateRsi(closes);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().BeInRange(0, 100);
    }

    [Fact]
    public void CalculateMacd_ShouldReturn_ValidResult()
    {
        var closes = GenerateCandles(50).Select(c => c.Close).ToList();
        var result = _sut.CalculateMacd(closes);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void CalculateBollingerBands_ShouldHave_UpperAboveLower()
    {
        var closes = GenerateCandles(50).Select(c => c.Close).ToList();
        var result = _sut.CalculateBollingerBands(closes);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Upper.Should().BeGreaterThan(result.Value.Lower);
        result.Value.Middle.Should().BeLessThan(result.Value.Upper);
        result.Value.Middle.Should().BeGreaterThan(result.Value.Lower);
    }

    [Fact]
    public void CalculateAtr_ShouldReturn_PositiveValue()
    {
        var candles = GenerateCandles(50);
        var result  = _sut.CalculateAtr(candles);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().BePositive();
    }

    [Fact]
    public void CalculateVwap_ShouldReturn_PriceInRange()
    {
        var candles = GenerateCandles(50);
        var result  = _sut.CalculateVwap(candles);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().BePositive();
    }

    [Fact]
    public void CalculateStochastic_ShouldReturn_KBetween0And100()
    {
        var candles = GenerateCandles(50);
        var result  = _sut.CalculateStochastic(candles);

        result.IsSuccess.Should().BeTrue();
        result.Value!.K.Should().BeInRange(0, 100);
        result.Value!.D.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Calculate_Full_ShouldSucceed_With50Candles()
    {
        var candles = GenerateCandles(50);
        var result  = _sut.Calculate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Rsi.Should().NotBeNull();
        result.Value!.Macd.Should().NotBeNull();
        result.Value!.BollingerBands.Should().NotBeNull();
        result.Value!.BullishCount().Should().BeInRange(0, 5);
    }

    [Fact]
    public void Calculate_ShouldFail_WithLessThan30Candles()
    {
        var candles = GenerateCandles(20);
        var result  = _sut.Calculate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CalculateParkinsonVolatility_ShouldReturn_PositiveValue()
    {
        var candles  = GenerateCandles(50);
        var parkVol  = _sut.CalculateParkinsonVolatility(candles);
        parkVol.Should().BePositive();
    }
}
