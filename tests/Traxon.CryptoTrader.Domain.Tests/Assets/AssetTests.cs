using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Domain.Tests.Assets;

public class AssetTests
{
    [Fact]
    public void All_ShouldContain_SevenAssets()
    {
        Asset.All.Should().HaveCount(7);
    }

    [Theory]
    [InlineData("BTCUSDT")]
    [InlineData("ETHUSDT")]
    [InlineData("SOLUSDT")]
    [InlineData("XRPUSDT")]
    [InlineData("DOGEUSDT")]
    [InlineData("BNBUSDT")]
    [InlineData("HYPEUSDT")]
    public void FromSymbol_ShouldReturn_CorrectAsset(string symbol)
    {
        var asset = Asset.FromSymbol(symbol);
        asset.Should().NotBeNull();
        asset!.Symbol.Should().Be(symbol);
    }

    [Fact]
    public void Equality_ShouldWork_ForSameSymbol()
    {
        Asset.BTCUSDT.Should().Be(Asset.BTCUSDT);
        Asset.BTCUSDT.Should().NotBe(Asset.ETHUSDT);
    }
}
