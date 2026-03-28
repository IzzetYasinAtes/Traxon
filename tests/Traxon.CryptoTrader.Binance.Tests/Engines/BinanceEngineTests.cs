using Binance.Net.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Binance.Abstractions;
using Traxon.CryptoTrader.Binance.Adapters;
using Traxon.CryptoTrader.Binance.Options;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Binance.Tests.Engines;

public class BinanceEngineTests
{
    private readonly IBinanceOrderService _orderService        = Substitute.For<IBinanceOrderService>();
    private readonly ITradeLogger         _tradeLogger         = Substitute.For<ITradeLogger>();
    private readonly IIndicatorCalculator _indicatorCalculator = Substitute.For<IIndicatorCalculator>();
    private readonly ICandleBuffer        _candleBuffer        = Substitute.For<ICandleBuffer>();

    private BinanceEngine CreateSut(bool enabled = true, IReadOnlyList<string>? allowedSymbols = null)
    {
        var opts = new BinanceOptions
        {
            Enabled        = enabled,
            MaxPositionSize = 100m,
            AllowedSymbols = allowedSymbols ?? ["BTCUSDT", "ETHUSDT"]
        };
        return new BinanceEngine(
            _orderService,
            _tradeLogger,
            _indicatorCalculator,
            _candleBuffer,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<BinanceEngine>.Instance);
    }

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

    private void SetupOrderServiceSuccess(long orderId = 12345L, decimal price = 50000m)
    {
        _orderService.GetCurrentPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Success(price));
        _orderService.PlaceMarketOrderAsync(
                Arg.Any<string>(), Arg.Any<OrderSide>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result<long>.Success(orderId));
    }

    [Fact]
    public async Task IsReady_WhenDisabled_ReturnsFailure()
    {
        var sut    = CreateSut(enabled: false);
        var result = await sut.IsReadyAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BinanceEngine.Disabled");
    }

    [Fact]
    public async Task IsReady_WhenEnabled_ReturnsSuccess()
    {
        var sut    = CreateSut(enabled: true);
        var result = await sut.IsReadyAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task OpenPosition_WhenDisabled_ReturnsFailure()
    {
        var sut    = CreateSut(enabled: false);
        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BinanceEngine.Disabled");
    }

    [Fact]
    public async Task OpenPosition_WhenSymbolNotAllowed_ReturnsFailure()
    {
        var sut    = CreateSut(enabled: true, allowedSymbols: ["ETHUSDT"]);
        var result = await sut.OpenPositionAsync(CreateUpSignal(Asset.BTCUSDT));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BinanceEngine.SymbolNotAllowed");
    }

    [Fact]
    public async Task OpenPosition_WhenOrderServiceSucceeds_LogsTrade()
    {
        SetupCandleBufferSuccess();
        SetupAtrSuccess();
        SetupOrderServiceSuccess(orderId: 12345L);

        var sut    = CreateSut();
        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsSuccess.Should().BeTrue();
        await _tradeLogger.Received(1).LogTradeOpenedAsync(Arg.Any<Trade>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenPosition_WhenOrderFails_ReturnsFailure()
    {
        SetupCandleBufferSuccess();
        SetupAtrSuccess();
        _orderService.GetCurrentPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<decimal>.Success(50000m));
        _orderService.PlaceMarketOrderAsync(
                Arg.Any<string>(), Arg.Any<OrderSide>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result<long>.Failure(new Error("Binance.OrderFailed", "Insufficient balance")));

        var sut    = CreateSut();
        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Binance.OrderFailed");
    }

    [Fact]
    public async Task CheckPositions_WhenTakeProfitHit_PlacesClosingOrder()
    {
        SetupCandleBufferSuccess(close: 50000m);
        SetupAtrSuccess(atr: 500m);
        SetupOrderServiceSuccess(orderId: 12345L, price: 51025m);

        var sut = CreateSut();
        await sut.OpenPositionAsync(CreateUpSignal());

        // entryPrice ≈ 50025, TP = 51025
        // candle.High = 51100 > 51025 → TP hit
        var candle = CreateCandle(high: 51100m, low: 49500m);
        await sut.CheckPositionsAsync(candle);

        var openTrades = (await sut.GetOpenTradesAsync()).Value!;
        openTrades.Should().BeEmpty();

        // 1 open + 1 close = 2 calls total
        await _orderService.Received(2).PlaceMarketOrderAsync(
            Arg.Any<string>(), Arg.Any<OrderSide>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenPosition_WithDuplicateAsset_ReturnsDuplicateError()
    {
        SetupCandleBufferSuccess();
        SetupAtrSuccess();
        SetupOrderServiceSuccess();

        var sut = CreateSut();
        await sut.OpenPositionAsync(CreateUpSignal());
        var result = await sut.OpenPositionAsync(CreateUpSignal());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Domain.DuplicatePosition");
    }
}
