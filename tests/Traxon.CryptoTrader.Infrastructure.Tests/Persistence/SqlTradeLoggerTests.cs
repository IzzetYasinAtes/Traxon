using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Indicators;
using Traxon.CryptoTrader.Domain.Market;
using Traxon.CryptoTrader.Domain.Trading;
using Traxon.CryptoTrader.Infrastructure.Persistence;

namespace Traxon.CryptoTrader.Infrastructure.Tests.Persistence;

public class SqlTradeLoggerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SqlTradeLoggerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _factory = new TestDbContextFactory(options);
    }

    public void Dispose() => _db.Dispose();

    private SqlTradeLogger CreateSut() => new SqlTradeLogger(
        _factory,
        NullLogger<SqlTradeLogger>.Instance);

    private static Trade CreateTrade() =>
        new Trade(
            engine:            "PaperPoly",
            asset:             Asset.BTCUSDT,
            timeFrame:         TimeFrame.FiveMinute,
            direction:         SignalDirection.Up,
            entryPrice:        0.51m,
            fairValue:         0.62m,
            edge:              0.11m,
            positionSize:      500m,
            kellyFraction:     0.05m,
            muEstimate:        0.001m,
            sigmaEstimate:     0.02m,
            regime:            MarketRegime.LowVolatility,
            indicatorSnapshot: "{\"rsi\":65}",
            entryReason:       "Test trade");

    [Fact]
    public async Task LogTradeOpenedAsync_PersistsTrade()
    {
        var sut   = CreateSut();
        var trade = CreateTrade();

        await sut.LogTradeOpenedAsync(trade);

        var saved = await _db.Trades.FindAsync(trade.Id);
        saved.Should().NotBeNull();
        saved!.Engine.Should().Be("PaperPoly");
        saved.EntryPrice.Should().Be(0.51m);
        saved.Status.Should().Be(TradeStatus.Open);
    }

    [Fact]
    public async Task LogTradeClosedAsync_UpdatesExistingTrade()
    {
        var sut   = CreateSut();
        var trade = CreateTrade();
        await sut.LogTradeOpenedAsync(trade);

        trade.Close(1.00m, TradeOutcome.Win, 50m);
        await sut.LogTradeClosedAsync(trade);

        var saved = await _db.Trades.FindAsync(trade.Id);
        saved.Should().NotBeNull();
        saved!.Status.Should().Be(TradeStatus.Closed);
        saved.Outcome.Should().Be(TradeOutcome.Win);
        saved.PnL.Should().Be(50m);
    }

    [Fact]
    public async Task LogPortfolioSnapshotAsync_SavesSnapshot()
    {
        var sut       = CreateSut();
        var portfolio = new Portfolio("PaperPoly", 10_000m);

        await sut.LogPortfolioSnapshotAsync(portfolio);

        var snapshots = await _db.PortfolioSnapshots.ToListAsync();
        snapshots.Should().ContainSingle();
        snapshots[0].Engine.Should().Be("PaperPoly");
        snapshots[0].Balance.Should().Be(10_000m);
        snapshots[0].TradeCount.Should().Be(0);
    }

    [Fact]
    public async Task LogSignalAsync_DoesNotThrow()
    {
        var sut    = CreateSut();
        var signal = new Signal(
            asset:         Asset.BTCUSDT,
            timeFrame:     TimeFrame.FiveMinute,
            direction:     SignalDirection.Up,
            fairValue:     0.62m,
            marketPrice:   0.50m,
            kellyFraction: 0.05m,
            muEstimate:    0.001m,
            sigmaEstimate: 0.02m,
            regime:        MarketRegime.LowVolatility,
            indicators:    MakeBullishIndicators());

        var act = async () => await sut.LogSignalAsync(signal);
        await act.Should().NotThrowAsync();
    }

    private static TechnicalIndicators MakeBullishIndicators() =>
        new TechnicalIndicators(
            asset:              Asset.BTCUSDT,
            timeFrame:          TimeFrame.FiveMinute,
            calculatedAt:       DateTime.UtcNow,
            currentPrice:       0.50m,
            rsi:                new RsiResult(65m),
            macd:               new MacdResult(0.01m, 0.005m, 0.005m),
            bollingerBands:     new BollingerBandsResult(0.55m, 0.50m, 0.45m),
            atr:                new AtrResult(0.01m),
            vwap:               new VwapResult(0.48m),
            stochastic:         new StochasticResult(70m, 60m),
            fastSma:            0.51m,
            slowSma:            0.49m,
            parkinsonVolatility: 0.02m);

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new AppDbContext(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new AppDbContext(_options));
    }
}
