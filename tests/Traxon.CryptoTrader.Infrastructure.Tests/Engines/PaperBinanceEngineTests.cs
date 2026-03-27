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
using Traxon.CryptoTrader.Infrastructure.Engines;

namespace Traxon.CryptoTrader.Infrastructure.Tests.Engines;

public class PaperBinanceEngineTests
{
    private readonly ITradeLogger         _tradeLogger         = Substitute.For<ITradeLogger>();
    private readonly IIndicatorCalculator _indicatorCalculator = Substitute.For<IIndicatorCalculator>();
    private readonly ICandleBuffer        _candleBuffer        = Substitute.For<ICandleBuffer>();

    private PaperBinanceEngine CreateSut() => new PaperBinanceEngine(
        _tradeLogger,
        _indicatorCalculator,
        _candleBuffer,
        NullLogger<PaperBinanceEngine>.Instance);

    private static Signal CreateUpSignal(Asset? asset = null) =>
        new Signal(
            asset:         asset ?? Asset.BTCUSDT,
            timeFrame:     TimeFrame.FiveMinute,
            direction:     SignalDirection.Up,
            fairValue:     0.62m,
            marketPrice:   0.50m,
            kellyFraction: 0.05m,
            muEstimate:    0.001m,
            sigmaEstimate: 0.02m,
            regime:        MarketRegime.LowVolatility,
            indicators:    MakeBullishIndicators());

    private static Candle CreateCandle(decimal high, decimal low, decimal open = 100m, decimal close = 105m, Asset? asset = null) =>
        new Candle(
            id:          DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            asset:       asset ?? Asset.BTCUSDT,
            timeFrame:   TimeFrame.FiveMinute,
            openTime:    DateTime.UtcNow.AddMinutes(-5),
            closeTime:   DateTime.UtcNow,
            open:        open,
            high:        high,
            low:         low,
            close:       close,
            volume:      1000m,
            quoteVolume: 100000m,
            tradeCount:  500,
            isClosed:    true);

    private static IReadOnlyList<Candle> CreateCandleList(int count = 50, decimal close = 50000m) =>
        Enumerable.Range(0, count)
            .Select(i => new Candle(
                id:          i,
                asset:       Asset.BTCUSDT,
                timeFrame:   TimeFrame.FiveMinute,
                openTime:    DateTime.UtcNow.AddMinutes(-count + i),
                closeTime:   DateTime.UtcNow.AddMinutes(-count + i + 5),
                open:        close - 100m,
                high:        close + 200m,
                low:         close - 200m,
                close:       close,
                volume:      1000m,
                quoteVolume: 50000000m,
                tradeCount:  500,
                isClosed:    true))
            .ToList();

    private static TechnicalIndicators MakeBullishIndicators() =>
        new TechnicalIndicators(
            asset:              Asset.BTCUSDT,
            timeFrame:          TimeFrame.FiveMinute,
            calculatedAt:       DateTime.UtcNow,
            currentPrice:       50000m,
            rsi:                new RsiResult(65m),
            macd:               new MacdResult(100m, 50m, 50m),
            bollingerBands:     new BollingerBandsResult(51000m, 50000m, 49000m),
            atr:                new AtrResult(500m),
            vwap:               new VwapResult(49500m),
            stochastic:         new StochasticResult(70m, 60m),
            fastSma:            50200m,
            slowSma:            49800m,
            parkinsonVolatility: 0.02m);

    private void SetupCandleBufferSuccess(decimal close = 50000m)
    {
        var candles = CreateCandleList(50, close);
        _candleBuffer.GetAll(Arg.Any<Asset>(), Arg.Any<TimeFrame>())
            .Returns(Result<IReadOnlyList<Candle>>.Success(candles));
    }

    private void SetupAtrSuccess(decimal atr = 500m)
    {
        _indicatorCalculator.CalculateAtr(Arg.Any<IReadOnlyList<Candle>>())
            .Returns(Result<AtrResult>.Success(new AtrResult(atr)));
    }

    [Fact]
    public async Task OpenPosition_WithValidSignal_SetsSlAndTp()
    {
        var sut = CreateSut();
        SetupCandleBufferSuccess(close: 50000m);
        SetupAtrSuccess(atr: 500m);

        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsSuccess.Should().BeTrue();
        var trade = result.Value!;
        // entryPrice = 50000 * (1 + 0.0005) = 50025
        // SL = entryPrice - (1.5 * 500) = 50025 - 750 = 49275
        // TP = entryPrice + (2.0 * 500) = 50025 + 1000 = 51025
        trade.IndicatorSnapshot.Should().Contain("sl");
        trade.IndicatorSnapshot.Should().Contain("tp");
        await _tradeLogger.Received(1).LogTradeOpenedAsync(Arg.Any<Trade>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckPositions_WhenTakeProfitHit_ClosesTradeAsWin()
    {
        var sut = CreateSut();
        SetupCandleBufferSuccess(close: 50000m);
        SetupAtrSuccess(atr: 500m);

        await sut.OpenPositionAsync(CreateUpSignal());

        // entryPrice ≈ 50025, TP = 51025
        // candle.High = 51100 > 51025 → TP hit
        var candle = CreateCandle(high: 51100m, low: 49500m);
        await sut.CheckPositionsAsync(candle);

        var openTrades = (await sut.GetOpenTradesAsync()).Value!;
        openTrades.Should().BeEmpty();

        await _tradeLogger.Received(1).LogTradeClosedAsync(
            Arg.Is<Trade>(t => t.Outcome == TradeOutcome.Win), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckPositions_WhenStopLossHit_ClosesTradeAsLoss()
    {
        var sut = CreateSut();
        SetupCandleBufferSuccess(close: 50000m);
        SetupAtrSuccess(atr: 500m);

        await sut.OpenPositionAsync(CreateUpSignal());

        // entryPrice ≈ 50025, SL = 49275
        // candle.Low = 49000 < 49275 → SL hit
        var candle = CreateCandle(high: 50100m, low: 49000m);
        await sut.CheckPositionsAsync(candle);

        var openTrades = (await sut.GetOpenTradesAsync()).Value!;
        openTrades.Should().BeEmpty();

        await _tradeLogger.Received(1).LogTradeClosedAsync(
            Arg.Is<Trade>(t => t.Outcome == TradeOutcome.Loss), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckPositions_WhenNeitherSlNorTpHit_TradeStaysOpen()
    {
        var sut = CreateSut();
        SetupCandleBufferSuccess(close: 50000m);
        SetupAtrSuccess(atr: 500m);

        await sut.OpenPositionAsync(CreateUpSignal());

        // entryPrice ≈ 50025, SL = 49275, TP = 51025
        // candle range: 49500 - 50500 → ne SL ne TP
        var candle = CreateCandle(high: 50500m, low: 49500m);
        await sut.CheckPositionsAsync(candle);

        var openTrades = (await sut.GetOpenTradesAsync()).Value!;
        openTrades.Should().HaveCount(1);
        await _tradeLogger.DidNotReceive().LogTradeClosedAsync(Arg.Any<Trade>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenPosition_WithDuplicateAsset_ReturnsDuplicateError()
    {
        var sut = CreateSut();
        SetupCandleBufferSuccess();
        SetupAtrSuccess();

        await sut.OpenPositionAsync(CreateUpSignal());
        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.DuplicatePosition");
    }

    [Fact]
    public async Task OpenPosition_WhenCandleBufferFails_ReturnsEngineNotReady()
    {
        var sut = CreateSut();
        _candleBuffer.GetAll(Arg.Any<Asset>(), Arg.Any<TimeFrame>())
            .Returns(Result<IReadOnlyList<Candle>>.Failure(Error.NotEnoughCandles));

        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.EngineNotReady");
    }
}
