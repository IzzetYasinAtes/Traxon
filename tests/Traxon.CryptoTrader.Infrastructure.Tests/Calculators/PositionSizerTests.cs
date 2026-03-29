using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Infrastructure.Calculators;

namespace Traxon.CryptoTrader.Infrastructure.Tests.Calculators;

public class PositionSizerTests
{
    [Fact]
    public void Calculate_WithSufficientEdge_ReturnsPositivePositionSize()
    {
        var sizer  = new PositionSizer();
        var result = sizer.Calculate(fairValue: 0.72m, marketPrice: 0.50m, bankroll: 10_000m);
        result.MeetsMinimumEdge.Should().BeTrue();
        result.PositionSize.Should().BeGreaterThan(0m);
        result.KellyFraction.Should().BeGreaterThan(0m);
        result.Edge.Should().BeApproximately(0.22m, 0.001m);
    }

    [Fact]
    public void Calculate_WithEdgeBelowMinimum_ReturnsMeetsMinimumEdgeFalse()
    {
        var sizer  = new PositionSizer();
        var result = sizer.Calculate(fairValue: 0.60m, marketPrice: 0.50m, bankroll: 10_000m);
        result.MeetsMinimumEdge.Should().BeFalse();
        result.PositionSize.Should().Be(0m);
    }

    [Fact]
    public void Calculate_PositionSize_NeverExceedsMaxPositionFraction()
    {
        var sizer    = new PositionSizer();
        var bankroll = 10_000m;
        var result   = sizer.Calculate(fairValue: 0.99m, marketPrice: 0.30m, bankroll: bankroll);
        result.PositionSize.Should().BeLessThanOrEqualTo(bankroll * 0.005m);
    }

    [Fact]
    public void Calculate_WithZeroBankroll_ReturnsZeroPositionSize()
    {
        var sizer  = new PositionSizer();
        var result = sizer.Calculate(fairValue: 0.65m, marketPrice: 0.45m, bankroll: 0m);
        result.MeetsMinimumEdge.Should().BeFalse();
        result.PositionSize.Should().Be(0m);
    }

    [Fact]
    public void Calculate_DownSignal_ReturnsPositivePositionSize()
    {
        var sizer  = new PositionSizer();
        var result = sizer.Calculate(fairValue: 0.28m, marketPrice: 0.50m, bankroll: 10_000m);
        result.MeetsMinimumEdge.Should().BeTrue();
        result.PositionSize.Should().BeGreaterThan(0m);
    }
}
