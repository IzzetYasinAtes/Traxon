using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Polymarket.Http;
using Traxon.CryptoTrader.Polymarket.Options;
using Traxon.CryptoTrader.Polymarket.Tests.Helpers;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Traxon.CryptoTrader.Polymarket.Tests.Http;

public sealed class PolymarketClientTests
{
    private static PolymarketClient CreateClient(
        string json,
        HttpStatusCode status = HttpStatusCode.OK,
        bool enabled = true)
    {
        var options = MSOptions.Create(new PolymarketOptions { Enabled = enabled });
        var http    = MockHttpMessageHandler.CreateClient(json, status);
        return new PolymarketClient(http, options, NullLogger<PolymarketClient>.Instance);
    }

    [Fact]
    public async Task GetOrderBookAsync_ValidResponse_ReturnsOrderBook()
    {
        const string json = """{"bids":[{"price":"0.45","size":"100"}],"asks":[{"price":"0.47","size":"50"}]}""";
        var client = CreateClient(json);

        var result = await client.GetOrderBookAsync("token123");

        result.IsSuccess.Should().BeTrue();
        result.Value!.BestBid.Should().Be(0.45m);
        result.Value!.BestAsk.Should().Be(0.47m);
        result.Value!.Midpoint.Should().Be(0.46m);
    }

    [Fact]
    public async Task GetMidpointAsync_ValidResponse_ReturnsDecimal()
    {
        const string json = """{"mid":"0.46"}""";
        var client = CreateClient(json);

        var result = await client.GetMidpointAsync("token123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0.46m);
    }

    [Fact]
    public async Task PlaceOrderAsync_WhenDisabled_ReturnsFailure()
    {
        var client = CreateClient("{}", enabled: false);
        var order  = new PolymarketOrderRequest
        {
            TokenId = "token123",
            Price   = 0.45m,
            Size    = 10m
        };

        var result = await client.PlaceOrderAsync(order);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Polymarket.Disabled");
    }

    [Fact]
    public async Task SendHeartbeatAsync_Returns200_ReturnsTrue()
    {
        const string json = """{}""";
        var client = CreateClient(json);

        var result = await client.SendHeartbeatAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrderBookAsync_HttpError_ReturnsFailure()
    {
        var client = CreateClient("{}", HttpStatusCode.InternalServerError);

        var result = await client.GetOrderBookAsync("token123");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Polymarket.HttpError");
    }
}
