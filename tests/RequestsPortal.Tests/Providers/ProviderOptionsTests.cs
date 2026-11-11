using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Options;
using Xunit;

namespace RequestsPortal.Tests.Providers;

public sealed class ProviderOptionsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(4294967.295)]
    public void Invalid_delay_is_rejected_before_processing(double seconds)
    {
        var options = new ProviderOptions { DelaySeconds = seconds };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Unknown_provider_mode_is_rejected()
    {
        var options = new ProviderOptions { Mode = (ProviderMode)999 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
