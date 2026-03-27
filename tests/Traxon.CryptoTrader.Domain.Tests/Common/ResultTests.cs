using FluentAssertions;
using Xunit;
using Traxon.CryptoTrader.Domain.Common;

namespace Traxon.CryptoTrader.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldHave_Value()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldHave_Error()
    {
        var result = Result<int>.Failure(Error.NotEnoughCandles);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotEnoughCandles);
        result.Value.Should().Be(default);
    }

    [Fact]
    public void Match_ShouldCall_CorrectBranch()
    {
        var success = Result<int>.Success(10);
        var output  = success.Match(v => $"ok:{v}", e => $"err:{e.Code}");
        output.Should().Be("ok:10");
    }
}
