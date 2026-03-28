using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Traxon.CryptoTrader.Polymarket.Http;
using Traxon.CryptoTrader.Polymarket.Options;
using Traxon.CryptoTrader.Polymarket.Tests.Helpers;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Traxon.CryptoTrader.Polymarket.Tests.Http;

public sealed class GammaApiClientTests
{
    private static GammaApiClient CreateClient(
        string json,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var options = MSOptions.Create(new PolymarketOptions
        {
            GammaApiUrl = "https://gamma-api.polymarket.com"
        });
        var http = MockHttpMessageHandler.CreateClient(json, status, "https://gamma-api.polymarket.com");
        return new GammaApiClient(http, options, NullLogger<GammaApiClient>.Instance);
    }

    [Fact]
    public async Task GetActiveCryptoMarketsAsync_ValidResponse_ReturnsMarkets()
    {
        const string json = """
        [
          {
            "conditionId": "cond1",
            "question": "Will BTC go Up by end of day?",
            "clobTokenIds": ["token_yes_1", "token_no_1"],
            "endDate": "2030-12-31T23:59:59Z",
            "active": true,
            "closed": false
          },
          {
            "conditionId": "cond2",
            "question": "Will ETH go Down by end of day?",
            "clobTokenIds": ["token_yes_2", "token_no_2"],
            "endDate": "2030-12-31T23:59:59Z",
            "active": true,
            "closed": false
          },
          {
            "conditionId": "cond3",
            "question": "Will SOL go Up by end of day?",
            "clobTokenIds": ["token_yes_3", "token_no_3"],
            "endDate": "2030-12-31T23:59:59Z",
            "active": true,
            "closed": false
          }
        ]
        """;

        var client = CreateClient(json);
        var result = await client.GetActiveCryptoMarketsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(3);
        result.Value!.First(m => m.ConditionId == "cond1").Direction.Should().Be("Up");
        result.Value!.First(m => m.ConditionId == "cond2").Direction.Should().Be("Down");
        result.Value!.First(m => m.ConditionId == "cond3").UnderlyingAsset.Should().Be("SOL");
    }

    [Fact]
    public async Task GetActiveCryptoMarketsAsync_EmptyArray_ReturnsEmptyList()
    {
        var client = CreateClient("[]");
        var result = await client.GetActiveCryptoMarketsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveCryptoMarketsAsync_HttpFailure_ReturnsFailure()
    {
        var client = CreateClient("{}", HttpStatusCode.ServiceUnavailable);
        var result = await client.GetActiveCryptoMarketsAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Gamma.HttpError");
    }
}
