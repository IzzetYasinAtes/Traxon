using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Domain.Assets;

namespace Traxon.CryptoTrader.Domain.Tests.Assets;

public class AssetTests
{
    [Fact]
    public void All_ShouldContain_ElevenAssets()
    {
        Asset.All.Should().HaveCount(11);
    }

    [Theory]
    [InlineData("BTCUSDT")]
    [InlineData("ETHUSDT")]
    [InlineData("SOLUSDT")]
    [InlineData("XRPUSDT")]
    [InlineData("DOGEUSDT")]
    [InlineData("AVAXUSDT")]
    [InlineData("BNBUSDT")]
    [InlineData("ADAUSDT")]
    [InlineData("DOTUSDT")]
    [InlineData("LINKUSDT")]
    [InlineData("MATICUSDT")]
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
