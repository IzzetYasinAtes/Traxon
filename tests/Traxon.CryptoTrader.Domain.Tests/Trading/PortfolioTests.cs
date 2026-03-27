using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Domain.Assets;
using Traxon.CryptoTrader.Domain.Trading;

namespace Traxon.CryptoTrader.Domain.Tests.Trading;

public class PortfolioTests
{
    private static Position MakePosition(decimal size) =>
        new(Asset.BTCUSDT, TimeFrame.FiveMinute, SignalDirection.Up,
            entryPrice: 0.50m, positionSize: size,
            stopLoss: null, takeProfit: null);

    [Fact]
    public void OpenPosition_ShouldReduceBalance()
    {
        var portfolio = new Portfolio("PaperPoly", 1000m);
        var position  = MakePosition(50m);

        var result = portfolio.OpenPosition(position);

        result.IsSuccess.Should().BeTrue();
        portfolio.Balance.Should().Be(950m);
        portfolio.OpenPositions.Should().HaveCount(1);
    }

    [Fact]
    public void OpenPosition_ShouldFail_WhenExceedsMaxPosition()
    {
        var portfolio    = new Portfolio("PaperPoly", 1000m);
        var bigPosition  = MakePosition(60m);

        var result = portfolio.OpenPosition(bigPosition);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ClosePosition_ShouldUpdateWinCount_OnWin()
    {
        var portfolio = new Portfolio("PaperPoly", 1000m);
        var position  = MakePosition(50m);
        portfolio.OpenPosition(position);

        portfolio.ClosePosition(position.Id, pnl: 10m, outcome: TradeOutcome.Win);

        portfolio.WinCount.Should().Be(1);
        portfolio.Balance.Should().Be(1010m);
    }
}
