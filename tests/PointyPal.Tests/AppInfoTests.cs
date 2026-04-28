using FluentAssertions;
using PointyPal.Infrastructure;
using Xunit;

namespace PointyPal.Tests;

public class AppInfoTests
{
    [Fact]
    public void AppInfo_UsesPt003ReleaseDefaults()
    {
        AppInfo.AppName.Should().Be("PointyPal");
        AppInfo.Version.Should().StartWith("0.21.0");
        AppInfo.BuildChannel.Should().Be("private-rc");
        AppInfo.ReleaseLabel.Should().Be("private-rc.1");
        AppInfo.BaselineDate.Should().Be("2026-04-27");
        AppInfo.WorkerContractVersion.Should().Be("1.1.0");
    }

    [Theory]
    [InlineData("dev", true)]
    [InlineData("private-rc", true)]
    [InlineData("production-preview", true)]
    [InlineData("beta", false)]
    [InlineData("", false)]
    public void IsValidBuildChannel_OnlyAllowsKnownChannels(string value, bool expected)
    {
        AppInfo.IsValidBuildChannel(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("0.21.0", true)]
    [InlineData("0.21.0-private-rc.1", true)]
    [InlineData("1.2", false)]
    [InlineData("v0.21.0", false)]
    [InlineData("", false)]
    public void IsValidVersionString_ValidatesSemverLikeVersion(string value, bool expected)
    {
        AppInfo.IsValidVersionString(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("private-rc.1", true)]
    [InlineData("production-preview", true)]
    [InlineData("", true)]
    [InlineData("-bad", false)]
    [InlineData("bad label", false)]
    public void IsValidReleaseLabel_ValidatesPackageSafeLabels(string value, bool expected)
    {
        AppInfo.IsValidReleaseLabel(value).Should().Be(expected);
    }
}
