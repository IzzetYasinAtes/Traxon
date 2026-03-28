using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Application.Polymarket.Models;
using Traxon.CryptoTrader.Domain.Common;
using Traxon.CryptoTrader.Polymarket.Options;
using Traxon.CryptoTrader.Polymarket.Services;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Traxon.CryptoTrader.Polymarket.Tests.Services;

public sealed class MarketDiscoveryServiceTests
{
    private static readonly long FutureDateSeconds =
        DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();

    private static readonly long PastDateSeconds =
        DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();

    private static MarketDiscoveryService CreateService(
        IGammaApiClient gammaClient,
        int minMinutes = 10)
    {
        var options = MSOptions.Create(new PolymarketOptions
        {
            MarketMinutesMinRemaining = minMinutes
        });
        return new MarketDiscoveryService(gammaClient, options, NullLogger<MarketDiscoveryService>.Instance);
    }

    [Fact]
    public async Task DiscoverMarketsAsync_FiltersClosedMarkets()
    {
        var gammaClient = Substitute.For<IGammaApiClient>();
        gammaClient.GetActiveCryptoMarketsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PolymarketMarket>>.Success(
                new List<PolymarketMarket>
                {
                    new() { ConditionId = "c1", Active = true,  Closed = false, Direction = "Up",   UnderlyingAsset = "BTC", EndDateUtcSeconds = FutureDateSeconds },
                    new() { ConditionId = "c2", Active = true,  Closed = true,  Direction = "Down", UnderlyingAsset = "ETH", EndDateUtcSeconds = FutureDateSeconds },
                    new() { ConditionId = "c3", Active = false, Closed = false, Direction = "Up",   UnderlyingAsset = "SOL", EndDateUtcSeconds = FutureDateSeconds }
                }.AsReadOnly()));

        var service = CreateService(gammaClient);
        var result  = await service.DiscoverMarketsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(1);
        result.Value!.First().ConditionId.Should().Be("c1");
    }

    [Fact]
    public async Task DiscoverMarketsAsync_FiltersExpiredMarkets()
    {
        var gammaClient = Substitute.For<IGammaApiClient>();
        gammaClient.GetActiveCryptoMarketsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PolymarketMarket>>.Success(
                new List<PolymarketMarket>
                {
                    new() { ConditionId = "c1", Active = true, Closed = false, Direction = "Up",   UnderlyingAsset = "BTC", EndDateUtcSeconds = FutureDateSeconds },
                    new() { ConditionId = "c2", Active = true, Closed = false, Direction = "Down", UnderlyingAsset = "ETH", EndDateUtcSeconds = PastDateSeconds }
                }.AsReadOnly()));

        var service = CreateService(gammaClient);
        var result  = await service.DiscoverMarketsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(1);
        result.Value!.Should().NotContain(m => m.ConditionId == "c2");
    }

    [Fact]
    public async Task DiscoverMarketsAsync_GammaClientFails_ReturnsFailure()
    {
        var gammaClient = Substitute.For<IGammaApiClient>();
        gammaClient.GetActiveCryptoMarketsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PolymarketMarket>>.Failure(
                new Error("Gamma.HttpError", "Service unavailable")));

        var service = CreateService(gammaClient);
        var result  = await service.DiscoverMarketsAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Gamma.HttpError");
    }
}
