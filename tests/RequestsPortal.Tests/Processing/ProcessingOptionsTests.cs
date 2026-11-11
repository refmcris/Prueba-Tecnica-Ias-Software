using RequestsPortal.Core.Options;
using Xunit;

namespace RequestsPortal.Tests.Processing;

public sealed class ProcessingOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void Invalid_poll_interval_is_rejected(double value)
    {
        var options = new ProcessingOptions { PollIntervalSeconds = value };
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(5, 5)]
    [InlineData(31, 30)]
    [InlineData(double.NaN, 30)]
    [InlineData(5, double.PositiveInfinity)]
    public void Invalid_timeout_or_lease_is_rejected(double timeout, double lease)
    {
        var options = new ProcessingOptions { TimeoutSeconds = timeout, LeaseSeconds = lease };
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_attempt_budget_is_rejected(int attempts)
    {
        var options = new ProcessingOptions { MaxAttempts = attempts };
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
