using Navarone.Languages.CSharp.Loading;

namespace Navarone.Tests.Languages;

public sealed class SolutionLoaderTests
{
    private const string Prefix = "Msbuild failed when processing the file '/repo/App/App.csproj' with message: ";

    [Theory]
    [InlineData(Prefix + "Package 'System.IdentityModel.Tokens.Jwt' 6.7.1 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-59j7-ghrg-fj52")]
    [InlineData(Prefix + "Package 'Newtonsoft.Json' 9.0.1 has a known high severity vulnerability, https://github.com/advisories/GHSA-5crp-9r3c-p9vr")]
    [InlineData(Prefix + "Error occurred while getting package vulnerability data: the source is offline")]
    [InlineData(Prefix + "Audit source 'https://api.nuget.org/v3/index.json' did not provide any vulnerability data.")]
    [InlineData("/repo/App/App.csproj : warning NU1903: Package 'X' 1.0.0 has a known high severity vulnerability")]
    public void ANuGetAuditWarningIsHidden(string message)
    {
        Assert.True(SolutionLoader.IsNuGetAudit(message));
    }

    [Theory]
    [InlineData(Prefix + "Package 'ImageLibrary 3.0.2' was restored using '.NETFramework,Version=v4.6.1' instead of the project target framework 'net10.0'. This package may not be fully compatible with your project.")]
    [InlineData(Prefix + "The SDK 'Microsoft.NET.Sdk.Web' specified could not be found.")]
    [InlineData(Prefix + "Package 'X' 1.0.0 has a known high severity vulnerability, https://example.org and the project failed to load")]
    [InlineData("/repo/App/App.csproj : error NU1101: Unable to find package NU1903.Helper")]
    [InlineData("/repo/App/App.csproj : warning NU1906: Package 'X' 1.0.0 has a future audit code")]
    [InlineData(Prefix + "error MSB4025: The project file could not be loaded. /repo/App/App.csproj : warning NU1903: Package 'X' 1.0.0 has a known high severity vulnerability")]
    public void EveryOtherFailureGoesToStderr(string message)
    {
        Assert.False(SolutionLoader.IsNuGetAudit(message));
    }
}
