using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Interfaces.Clients.SpotApi;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Traxon.CryptoTrader.Binance.Services;

namespace Traxon.CryptoTrader.Binance.Tests.Services;

public class BinanceOrderServiceTests
{
    private readonly IBinanceRestClient                   _restClient      = Substitute.For<IBinanceRestClient>();
    private readonly IBinanceRestClientSpotApi            _spotApi         = Substitute.For<IBinanceRestClientSpotApi>();
    private readonly IBinanceRestClientSpotApiTrading     _spotTrading     = Substitute.For<IBinanceRestClientSpotApiTrading>();
    private readonly IBinanceRestClientSpotApiExchangeData _spotExchange   = Substitute.For<IBinanceRestClientSpotApiExchangeData>();

    private BinanceOrderService CreateSut()
    {
        _restClient.SpotApi.Returns(_spotApi);
        _spotApi.Trading.Returns(_spotTrading);
        _spotApi.ExchangeData.Returns(_spotExchange);
        return new BinanceOrderService(_restClient, NullLogger<BinanceOrderService>.Instance);
    }

    private static WebCallResult<T> SuccessResult<T>(T data) =>
        new(null, null, null, null, null, null, null, null, null, null!,
            null, ResultDataSource.Server, data, null);

    private static WebCallResult<T> ErrorResult<T>() =>
        new(new ServerError(
            new ErrorInfo(ErrorType.InvalidParameter, "Test error"),
            null));

    [Fact]
    public async Task PlaceMarketOrder_WhenApiSucceeds_ReturnsOrderId()
    {
        var sut        = CreateSut();
        var placedOrder = new BinancePlacedOrder { Id = 12345L };

        _spotTrading.PlaceOrderAsync(
                symbol: Arg.Any<string>(),
                side: Arg.Any<OrderSide>(),
                type: Arg.Any<SpotOrderType>(),
                quantity: Arg.Any<decimal?>(),
                quoteQuantity: Arg.Any<decimal?>(),
                newClientOrderId: Arg.Any<string?>(),
                price: Arg.Any<decimal?>(),
                timeInForce: Arg.Any<TimeInForce?>(),
                stopPrice: Arg.Any<decimal?>(),
                icebergQty: Arg.Any<decimal?>(),
                orderResponseType: Arg.Any<OrderResponseType?>(),
                trailingDelta: Arg.Any<int?>(),
                strategyId: Arg.Any<int?>(),
                strategyType: Arg.Any<int?>(),
                selfTradePreventionMode: Arg.Any<SelfTradePreventionMode?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SuccessResult(placedOrder)));

        var result = await sut.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.001m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(12345L);
    }

    [Fact]
    public async Task PlaceMarketOrder_WhenApiFails_ReturnsFailure()
    {
        var sut = CreateSut();

        _spotTrading.PlaceOrderAsync(
                symbol: Arg.Any<string>(),
                side: Arg.Any<OrderSide>(),
                type: Arg.Any<SpotOrderType>(),
                quantity: Arg.Any<decimal?>(),
                quoteQuantity: Arg.Any<decimal?>(),
                newClientOrderId: Arg.Any<string?>(),
                price: Arg.Any<decimal?>(),
                timeInForce: Arg.Any<TimeInForce?>(),
                stopPrice: Arg.Any<decimal?>(),
                icebergQty: Arg.Any<decimal?>(),
                orderResponseType: Arg.Any<OrderResponseType?>(),
                trailingDelta: Arg.Any<int?>(),
                strategyId: Arg.Any<int?>(),
                strategyType: Arg.Any<int?>(),
                selfTradePreventionMode: Arg.Any<SelfTradePreventionMode?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ErrorResult<BinancePlacedOrder>()));

        var result = await sut.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.001m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Binance.OrderFailed");
    }

    [Fact]
    public async Task PlaceMarketOrder_WhenApiThrows_ReturnsFailure()
    {
        var sut = CreateSut();

        _spotTrading.PlaceOrderAsync(
                symbol: Arg.Any<string>(),
                side: Arg.Any<OrderSide>(),
                type: Arg.Any<SpotOrderType>(),
                quantity: Arg.Any<decimal?>(),
                quoteQuantity: Arg.Any<decimal?>(),
                newClientOrderId: Arg.Any<string?>(),
                price: Arg.Any<decimal?>(),
                timeInForce: Arg.Any<TimeInForce?>(),
                stopPrice: Arg.Any<decimal?>(),
                icebergQty: Arg.Any<decimal?>(),
                orderResponseType: Arg.Any<OrderResponseType?>(),
                trailingDelta: Arg.Any<int?>(),
                strategyId: Arg.Any<int?>(),
                strategyType: Arg.Any<int?>(),
                selfTradePreventionMode: Arg.Any<SelfTradePreventionMode?>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<WebCallResult<BinancePlacedOrder>>(new InvalidOperationException("Network failure")));

        var result = await sut.PlaceMarketOrderAsync("BTCUSDT", OrderSide.Buy, 0.001m);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Binance.Exception");
    }

    [Fact]
    public async Task GetCurrentPrice_WhenApiSucceeds_ReturnsPrice()
    {
        var sut       = CreateSut();
        var binancePrice = new BinancePrice { Price = 50000m };

        _spotExchange.GetPriceAsync(
                symbol: Arg.Any<string>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SuccessResult(binancePrice)));

        var result = await sut.GetCurrentPriceAsync("BTCUSDT");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(50000m);
    }

    [Fact]
    public async Task GetCurrentPrice_WhenApiFails_ReturnsFailure()
    {
        var sut = CreateSut();

        _spotExchange.GetPriceAsync(
                symbol: Arg.Any<string>(),
                ct: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ErrorResult<BinancePrice>()));

        var result = await sut.GetCurrentPriceAsync("BTCUSDT");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Binance.PriceFailed");
    }
}
