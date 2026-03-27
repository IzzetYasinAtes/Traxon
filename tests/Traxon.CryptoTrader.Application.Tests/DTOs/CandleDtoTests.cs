using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Application.DTOs;

namespace Traxon.CryptoTrader.Application.Tests.DTOs;

public class CandleDtoTests
{
    [Fact]
    public void CandleDto_ShouldStore_AllProperties()
    {
        var openTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CandleDto(
            Symbol: "BTCUSDT",
            Interval: "5m",
            OpenTime: openTime,
            Open: 100m,
            High: 105m,
            Low: 98m,
            Close: 103m,
            Volume: 1000m,
            IsClosed: true);

        dto.Symbol.Should().Be("BTCUSDT");
        dto.Interval.Should().Be("5m");
        dto.OpenTime.Should().Be(openTime);
        dto.Open.Should().Be(100m);
        dto.High.Should().Be(105m);
        dto.Low.Should().Be(98m);
        dto.Close.Should().Be(103m);
        dto.Volume.Should().Be(1000m);
        dto.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void CandleDto_Equality_ShouldBeValueBased()
    {
        var openTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto1 = new CandleDto("BTCUSDT", "5m", openTime, 100m, 105m, 98m, 103m, 1000m, true);
        var dto2 = new CandleDto("BTCUSDT", "5m", openTime, 100m, 105m, 98m, 103m, 1000m, true);

        dto1.Should().Be(dto2);
    }
}
