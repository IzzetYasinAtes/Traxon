using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Application.Mappings;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Application.Tests.Mappings;

public sealed class DomainMapperTests
{
    private static TechnicalIndicators CreateIndicators(
        Asset asset, TimeFrame tf, decimal price = 50_000m,
        decimal rsi = 55m, decimal macdHistogram = 0.001m,
        decimal fastSma = 50_100m, decimal slowSma = 49_900m,
        decimal vwap = 49_000m)
    {
        return new TechnicalIndicators(
            asset, tf, DateTime.UtcNow, price,
            new RsiResult(rsi),
            new MacdResult(0.005m, 0.004m, macdHistogram),
            new BollingerBandsResult(51_000m, 50_000m, 49_000m),
            new AtrResult(500m),
            new VwapResult(vwap),
            new StochasticResult(65m, 60m),
            fastSma, slowSma,
            parkinsonVolatility: 0.02m);
    }

    private static Signal CreateSignal(
        SignalDirection direction = SignalDirection.Up,
        MarketRegime regime = MarketRegime.LowVolatility)
    {
        var asset = Asset.BTCUSDT;
        var tf = TimeFrame.FiveMinute;
        var indicators = CreateIndicators(asset, tf);

        return new Signal(
            asset, tf, direction,
            fairValue: 0.55m,
            marketPrice: 0.50m,
            kellyFraction: 0.10m,
            muEstimate: 0.001m,
            sigmaEstimate: 0.02m,
            regime,
            indicators);
    }

    private static Trade CreateTrade(TradeStatus status = TradeStatus.Open)
    {
        var trade = new Trade(
            engine: "PaperBinance",
            asset: Asset.ETHUSDT,
            timeFrame: TimeFrame.FifteenMinute,
            direction: SignalDirection.Down,
            entryPrice: 3_200m,
            fairValue: 0.45m,
            edge: 0.05m,
            positionSize: 100m,
            kellyFraction: 0.08m,
            muEstimate: -0.001m,
            sigmaEstimate: 0.025m,
            regime: MarketRegime.HighVolatility,
            indicatorSnapshot: "{}",
            entryReason: "edge>0.03");

        if (status == TradeStatus.Closed)
            trade.Close(3_300m, TradeOutcome.Win, pnl: 100m);

        return trade;
    }

    private static Portfolio CreatePortfolio(string engine = "PaperBinance")
    {
        return new Portfolio(engine, initialBalance: 10_000m);
    }

    // ---- Signal.ToDto() ----

    [Fact]
    public void Signal_ToDto_MapsAllFieldsCorrectly()
    {
        var signal = CreateSignal(SignalDirection.Up, MarketRegime.LowVolatility);

        var dto = signal.ToDto();

        dto.SignalId.Should().Be(signal.SignalId);
        dto.Symbol.Should().Be("BTCUSDT");
        dto.Interval.Should().Be("5m");
        dto.Direction.Should().Be("UP");
        dto.FairValue.Should().Be(signal.FairValue);
        dto.MarketPrice.Should().Be(signal.MarketPrice);
        dto.Edge.Should().Be(signal.Edge);
        dto.KellyFraction.Should().Be(signal.KellyFraction);
        dto.Regime.Should().Be("LOW_VOL");
        dto.Rsi.Should().Be(signal.Indicators.Rsi.Value);
        dto.MacdHistogram.Should().Be(signal.Indicators.Macd.Histogram);
        dto.BullishCount.Should().Be(signal.Indicators.BullishCount());
    }

    [Fact]
    public void Signal_ToDto_DownDirection_MapsCorrectly()
    {
        var signal = CreateSignal(SignalDirection.Down, MarketRegime.HighVolatility);

        var dto = signal.ToDto();

        dto.Direction.Should().Be("DOWN");
        dto.Regime.Should().Be("HIGH_VOL");
    }

    // ---- Trade.ToDto() ----

    [Fact]
    public void Trade_ToDto_OpenTrade_MapsCorrectly()
    {
        var trade = CreateTrade(TradeStatus.Open);

        var dto = trade.ToDto();

        dto.TradeId.Should().Be(trade.Id);
        dto.Engine.Should().Be("PaperBinance");
        dto.Symbol.Should().Be("ETHUSDT");
        dto.Interval.Should().Be("15m");
        dto.Direction.Should().Be("DOWN");
        dto.EntryPrice.Should().Be(3_200m);
        dto.ExitPrice.Should().BeNull();
        dto.Status.Should().Be("OPEN");
        dto.Outcome.Should().BeNull();
        dto.PnL.Should().BeNull();
        dto.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Trade_ToDto_ClosedTrade_MapsOutcomeAndPnL()
    {
        var trade = CreateTrade(TradeStatus.Closed);

        var dto = trade.ToDto();

        dto.Status.Should().Be("CLOSED");
        dto.Outcome.Should().Be("WIN");
        dto.PnL.Should().NotBeNull();
        dto.ExitPrice.Should().NotBeNull();
        dto.ClosedAt.Should().NotBeNull();
    }

    // ---- Portfolio.ToDto() ----

    [Fact]
    public void Portfolio_ToDto_EmptyPortfolio_MapsCorrectly()
    {
        var portfolio = CreatePortfolio("TestEngine");

        var dto = portfolio.ToDto();

        dto.Engine.Should().Be("TestEngine");
        dto.Balance.Should().Be(10_000m);
        dto.InitialBalance.Should().Be(10_000m);
        dto.TotalPnL.Should().Be(0m);
        dto.WinCount.Should().Be(0);
        dto.LossCount.Should().Be(0);
        dto.TotalTrades.Should().Be(0);
        dto.WinRate.Should().Be(0m);
        dto.OpenPositionCount.Should().Be(0);
    }

    // ---- Candle.ToCandleDto() ----

    [Fact]
    public void Candle_ToCandleDto_MapsAllFieldsCorrectly()
    {
        var now = DateTime.UtcNow;
        var candle = new Candle(
            id: 1,
            asset: Asset.SOLUSDT,
            timeFrame: TimeFrame.FiveMinute,
            openTime: now,
            closeTime: now.AddMinutes(5),
            open: 180m,
            high: 185m,
            low: 178m,
            close: 183m,
            volume: 10_000m,
            quoteVolume: 1_800_000m,
            tradeCount: 500,
            isClosed: true);

        var dto = candle.ToCandleDto();

        dto.Symbol.Should().Be("SOLUSDT");
        dto.Interval.Should().Be("5m");
        dto.OpenTime.Should().Be(now);
        dto.Open.Should().Be(180m);
        dto.High.Should().Be(185m);
        dto.Low.Should().Be(178m);
        dto.Close.Should().Be(183m);
        dto.Volume.Should().Be(10_000m);
        dto.IsClosed.Should().BeTrue();
    }
}
