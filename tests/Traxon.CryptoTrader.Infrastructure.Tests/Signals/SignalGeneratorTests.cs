using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Infrastructure.Signals;

namespace Traxon.CryptoTrader.Infrastructure.Tests.Signals;

public class SignalGeneratorTests
{
    private readonly IIndicatorCalculator    _indicatorCalc = Substitute.For<IIndicatorCalculator>();
    private readonly IFairValueCalculator    _fairValueCalc = Substitute.For<IFairValueCalculator>();
    private readonly IPositionSizer          _positionSizer = Substitute.For<IPositionSizer>();

    private SignalGenerator CreateSut() => new SignalGenerator(
        _indicatorCalc,
        _fairValueCalc,
        _positionSizer,
        NullLogger<SignalGenerator>.Instance);

    private static List<Candle> CreateCandles(int count = 50) =>
        Enumerable.Range(0, count)
            .Select(i => new Candle(
                id: i,
                asset: Asset.BTCUSDT,
                timeFrame: TimeFrame.FiveMinute,
                openTime: DateTime.UtcNow.AddMinutes(-count + i),
                closeTime: DateTime.UtcNow.AddMinutes(-count + i + 5),
                open: 0.50m, high: 0.55m, low: 0.45m, close: 0.50m,
                volume: 1000m, quoteVolume: 500m, tradeCount: 100, isClosed: true))
            .ToList();

    private static TechnicalIndicators MakeBullishIndicators() =>
        new TechnicalIndicators(
            asset: Asset.BTCUSDT,
            timeFrame: TimeFrame.FiveMinute,
            calculatedAt: DateTime.UtcNow,
            currentPrice: 0.50m,
            rsi: new RsiResult(65m),
            macd: new MacdResult(0.01m, 0.005m, 0.005m),
            bollingerBands: new BollingerBandsResult(0.55m, 0.50m, 0.45m),
            atr: new AtrResult(0.01m),
            vwap: new VwapResult(0.48m),
            stochastic: new StochasticResult(70m, 60m),
            fastSma: 0.51m,
            slowSma: 0.49m,
            parkinsonVolatility: 0.02m);

    [Fact]
    public void Generate_WithBullishSignalAndSufficientEdge_ReturnsSignal()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(50);

        _indicatorCalc.Calculate(Arg.Any<Asset>(), Arg.Any<TimeFrame>(), Arg.Any<IReadOnlyList<Candle>>())
            .Returns(Result<TechnicalIndicators>.Success(MakeBullishIndicators()));
        _fairValueCalc.Calculate(Arg.Any<IReadOnlyList<Candle>>(), Arg.Any<TimeFrame>())
            .Returns(new FairValueResult(0.62m, 0.005m, 0.02m, 0.3m));
        _positionSizer.Calculate(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new PositionSizeResult(0.06m, 300m, 0.12m, true));
        _indicatorCalc.CalculateParkinsonVolatility(Arg.Any<IReadOnlyList<Candle>>(), Arg.Any<int>())
            .Returns(0.02m);

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.50m);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Direction.Should().Be(SignalDirection.Up);
        result.Value!.FairValue.Should().Be(0.62m);
    }

    [Fact]
    public void Generate_WithNotEnoughCandles_ReturnsNotEnoughCandlesFailure()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(30);

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.50m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.NotEnoughCandles");
    }

    [Fact]
    public void Generate_WithMarketPriceBelowRange_ReturnsInvalidMarketPriceFailure()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(50);

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.20m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.InvalidMarketPrice");
    }

    [Fact]
    public void Generate_WithMarketPriceAboveRange_ReturnsInvalidMarketPriceFailure()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(50);

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.70m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.InvalidMarketPrice");
    }

    [Fact]
    public void Generate_WhenEdgeBelowMinimum_ReturnsInvalidEdgeFailure()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(50);

        _indicatorCalc.Calculate(Arg.Any<Asset>(), Arg.Any<TimeFrame>(), Arg.Any<IReadOnlyList<Candle>>())
            .Returns(Result<TechnicalIndicators>.Success(MakeBullishIndicators()));
        _fairValueCalc.Calculate(Arg.Any<IReadOnlyList<Candle>>(), Arg.Any<TimeFrame>())
            .Returns(new FairValueResult(0.51m, 0.001m, 0.02m, 0.05m));
        _positionSizer.Calculate(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new PositionSizeResult(0m, 0m, 0.01m, false));
        _indicatorCalc.CalculateParkinsonVolatility(Arg.Any<IReadOnlyList<Candle>>(), Arg.Any<int>())
            .Returns(0.02m);

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.50m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.InvalidEdge");
    }

    [Fact]
    public void Generate_WhenFairValueDirectionMismatch_ReturnsSignalDirectionMismatchFailure()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(50);

        _indicatorCalc.Calculate(Arg.Any<Asset>(), Arg.Any<TimeFrame>(), Arg.Any<IReadOnlyList<Candle>>())
            .Returns(Result<TechnicalIndicators>.Success(MakeBullishIndicators()));
        _fairValueCalc.Calculate(Arg.Any<IReadOnlyList<Candle>>(), Arg.Any<TimeFrame>())
            .Returns(new FairValueResult(0.45m, -0.003m, 0.02m, -0.25m));
        _indicatorCalc.CalculateParkinsonVolatility(Arg.Any<IReadOnlyList<Candle>>(), Arg.Any<int>())
            .Returns(0.02m);

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.50m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.SignalDirectionMismatch");
    }

    [Fact]
    public void Generate_WhenIndicatorCalculationFails_ReturnsIndicatorFailure()
    {
        var sut     = CreateSut();
        var candles = CreateCandles(50);

        _indicatorCalc.Calculate(Arg.Any<Asset>(), Arg.Any<TimeFrame>(), Arg.Any<IReadOnlyList<Candle>>())
            .Returns(Result<TechnicalIndicators>.Failure(Error.NotEnoughCandles));

        var result = sut.Generate(Asset.BTCUSDT, TimeFrame.FiveMinute, candles, marketPrice: 0.50m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.NotEnoughCandles");
    }
}
