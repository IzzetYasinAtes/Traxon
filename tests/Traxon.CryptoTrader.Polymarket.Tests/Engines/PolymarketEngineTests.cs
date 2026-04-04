using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Traxon.CryptoTrader.Application.Abstractions;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Polymarket.Engines;
using Traxon.CryptoTrader.Polymarket.Options;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Traxon.CryptoTrader.Polymarket.Tests.Engines;

public sealed class PolymarketEngineTests
{
    private readonly IPolymarketClient        _client        = Substitute.For<IPolymarketClient>();
    private readonly IPolymarketSigningClient _signingClient = Substitute.For<IPolymarketSigningClient>();
    private readonly IMarketDiscoveryService  _discovery     = Substitute.For<IMarketDiscoveryService>();
    private readonly ITradeLogger             _tradeLogger   = Substitute.For<ITradeLogger>();

    private PolymarketEngine CreateSut(bool enabled = false) =>
        new PolymarketEngine(
            _client,
            _signingClient,
            _discovery,
            MSOptions.Create(new PolymarketOptions { Enabled = enabled }),
            _tradeLogger,
            NullLogger<PolymarketEngine>.Instance);

    private static Signal CreateSignal() =>
        new Signal(
            asset:         Asset.BTCUSDT,
            timeFrame:     TimeFrame.FiveMinute,
            direction:     SignalDirection.Up,
            fairValue:     0.62m,
            marketPrice:   0.50m,
            kellyFraction: 0.05m,
            muEstimate:    0.001m,
            sigmaEstimate: 0.02m,
            regime:        MarketRegime.LowVolatility,
            indicators:    MakeIndicators());

    private static TechnicalIndicators MakeIndicators() =>
        new TechnicalIndicators(
            asset:               Asset.BTCUSDT,
            timeFrame:           TimeFrame.FiveMinute,
            calculatedAt:        DateTime.UtcNow,
            currentPrice:        0.50m,
            rsi:                 new RsiResult(65m),
            macd:                new MacdResult(0.01m, 0.005m, 0.005m),
            bollingerBands:      new BollingerBandsResult(0.55m, 0.50m, 0.45m),
            atr:                 new AtrResult(0.01m),
            vwap:                new VwapResult(0.48m),
            stochastic:          new StochasticResult(70m, 60m),
            fastSma:             0.51m,
            slowSma:             0.49m,
            parkinsonVolatility: 0.02m);

    [Fact]
    public async Task IsReadyAsync_WhenDisabled_ReturnsFalse()
    {
        var sut    = CreateSut(enabled: false);
        var result = await sut.IsReadyAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Engine.Disabled");
    }

    [Fact]
    public async Task OpenPositionAsync_WhenDisabled_ReturnsError()
    {
        var sut    = CreateSut(enabled: false);
        var signal = CreateSignal();

        var result = await sut.OpenPositionAsync(signal);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Engine.Disabled");
    }

    [Fact]
    public async Task GetOpenTradesAsync_Initially_ReturnsEmpty()
    {
        var sut    = CreateSut();
        var result = await sut.GetOpenTradesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetPortfolioAsync_ReturnsPortfolio()
    {
        var sut    = CreateSut();
        var result = await sut.GetPortfolioAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Engine.Should().Be("LivePoly");
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var sut = CreateSut();
        var act = async () => await sut.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
