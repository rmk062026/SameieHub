using System.Reflection;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

public class AppVersionTests
{
    [Theory]
    [InlineData("0.8.5", "0.8.5")]
    [InlineData("0.8.5-test.1", "0.8.5-test.1")]
    [InlineData("0.8.5-test.1+3f2a9c1d8e7b6a5f4e3d2c1b0a9f8e7d6c5b4a39", "0.8.5-test.1")]
    [InlineData("1.0.0+abc", "1.0.0")]
    public void TheCommitMarkThatTheBuildAddsIsNotShown(string informationalVersion, string expected)
    {
        Assert.Equal(expected, AppVersion.Clean(informationalVersion, fallback: "x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+abc")]
    public void WithoutAUsableVersion_TheFallbackIsUsed(string? informationalVersion)
    {
        Assert.Equal("1.2.3", AppVersion.Clean(informationalVersion, fallback: "1.2.3"));
    }

    [Fact]
    public void TheVersionComesFromTheProgramsOwnVersionInformation_NotFromATextInTheCode()
    {
        var assembly = typeof(AppVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var shown = AppVersion.Describe(assembly);

        Assert.False(string.IsNullOrWhiteSpace(shown));
        Assert.DoesNotContain('+', shown);
        Assert.StartsWith(shown, informational);
    }
}
