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
        // Events endpoint returns array of events, each with nested markets array.
        // clobTokenIds and outcomes are JSON-encoded strings.
        // The mock returns the same response for all 7 assets x 3 timestamps = 21 requests,
        // each producing 2 entries (Up + Down) = 42 total markets.
        const string json = """
        [
          {
            "markets": [
              {
                "conditionId": "cond1",
                "question": "Will BTC go Up?",
                "clobTokenIds": "[\"token_yes_1\", \"token_no_1\"]",
                "outcomes": "[\"Up\", \"Down\"]",
                "endDate": "2030-12-31T23:59:59Z",
                "active": true,
                "closed": false
              }
            ]
          }
        ]
        """;

        var client = CreateClient(json);
        var result = await client.GetActiveCryptoMarketsAsync();

        result.IsSuccess.Should().BeTrue();
        // 7 assets x 3 timestamps x 1 market x 2 directions = 42 entries
        result.Value!.Count.Should().Be(42);
        result.Value!.Should().Contain(m => m.Direction == "Up");
        result.Value!.Should().Contain(m => m.Direction == "Down");
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
    public async Task GetActiveCryptoMarketsAsync_HttpFailure_ReturnsEmptyList()
    {
        // Non-success HTTP status for individual slug requests is handled gracefully
        // (continue to next slug), so the overall result is still Success with empty list.
        var client = CreateClient("{}", HttpStatusCode.ServiceUnavailable);
        var result = await client.GetActiveCryptoMarketsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(0);
    }
}
