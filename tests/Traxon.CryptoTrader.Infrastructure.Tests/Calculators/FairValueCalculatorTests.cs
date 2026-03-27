using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Infrastructure.Calculators;

namespace Traxon.CryptoTrader.Infrastructure.Tests.Calculators;

public class FairValueCalculatorTests
{
    private static List<Candle> CreateCandles(int count, decimal startPrice, decimal trend)
    {
        var candles = new List<Candle>();
        var price = startPrice;
        for (int i = 0; i < count; i++)
        {
            price += trend;
            var open  = price - Math.Abs(trend) * 0.5m;
            var high  = price + Math.Abs(trend) * 2m;
            var low   = price - Math.Abs(trend) * 2m;
            candles.Add(new Candle(
                id: i,
                asset: Asset.BTCUSDT,
                timeFrame: TimeFrame.FiveMinute,
                openTime: DateTime.UtcNow.AddMinutes(-count + i),
                closeTime: DateTime.UtcNow.AddMinutes(-count + i + 5),
                open: Math.Max(0.01m, open),
                high: Math.Max(0.01m, high),
                low: Math.Max(0.01m, low),
                close: Math.Max(0.01m, price),
                volume: 1000m,
                quoteVolume: 1000m * price,
                tradeCount: 100,
                isClosed: true));
        }
        return candles;
    }

    [Fact]
    public void Calculate_WithUpTrend_ReturnsFairValueAboveHalf()
    {
        var calc    = new FairValueCalculator();
        var candles = CreateCandles(50, 0.40m, trend: +0.005m);
        var result  = calc.Calculate(candles, TimeFrame.FiveMinute);
        result.FairValue.Should().BeGreaterThan(0.50m);
        result.Mu.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Calculate_WithDownTrend_ReturnsFairValueBelowHalf()
    {
        var calc    = new FairValueCalculator();
        var candles = CreateCandles(50, 0.60m, trend: -0.005m);
        var result  = calc.Calculate(candles, TimeFrame.FiveMinute);
        result.FairValue.Should().BeLessThan(0.50m);
        result.Mu.Should().BeLessThan(0m);
    }

    [Fact]
    public void Calculate_WithFlatMarket_ReturnsFairValueNearHalf()
    {
        var calc    = new FairValueCalculator();
        var candles = CreateCandles(50, 0.50m, trend: 0m);
        var result  = calc.Calculate(candles, TimeFrame.FiveMinute);
        result.FairValue.Should().BeInRange(0.40m, 0.60m);
    }

    [Fact]
    public void Calculate_FairValueAlwaysClamped_Between001And099()
    {
        var calc = new FairValueCalculator();

        var extremeUp = CreateCandles(50, 0.01m, trend: +0.10m);
        var result1   = calc.Calculate(extremeUp, TimeFrame.FiveMinute);
        result1.FairValue.Should().BeInRange(0.01m, 0.99m);

        var extremeDown = CreateCandles(50, 5.00m, trend: -0.10m);
        var result2     = calc.Calculate(extremeDown, TimeFrame.FiveMinute);
        result2.FairValue.Should().BeInRange(0.01m, 0.99m);
    }

    [Fact]
    public void CalculateMomentum_WithRisingPrices_ReturnsPositiveValue()
    {
        var calc   = new FairValueCalculator();
        var closes = Enumerable.Range(1, 20).Select(i => 0.40m + i * 0.01m).ToList();
        var mu     = calc.CalculateMomentum(closes);
        mu.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CalculateMomentum_WithFallingPrices_ReturnsNegativeValue()
    {
        var calc   = new FairValueCalculator();
        var closes = Enumerable.Range(1, 20).Select(i => 0.60m - i * 0.01m).ToList();
        var mu     = calc.CalculateMomentum(closes);
        mu.Should().BeLessThan(0m);
    }

    [Fact]
    public void CalculateMomentum_WithInsufficientData_ReturnsZero()
    {
        var calc   = new FairValueCalculator();
        var closes = new List<decimal> { 0.50m, 0.51m };
        var mu     = calc.CalculateMomentum(closes);
        mu.Should().Be(0m);
    }
}
