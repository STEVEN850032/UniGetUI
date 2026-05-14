#if WINDOWS
using UniGetUI.PackageEngine.Managers.PowerShellManager;
using UniGetUI.PackageEngine.Managers.PowerShell7Manager;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Serializable;
using UniGetUI.PackageEngine.Structs;
using UniGetUI.PackageEngine.Tests.Infrastructure.Assertions;
using UniGetUI.PackageEngine.Tests.Infrastructure.Builders;

namespace UniGetUI.PackageEngine.Tests;

public sealed class PowerShellManagerTests
{
    [Fact]
    public void ParseInstalledPackages_BuildsPackagesFromModuleTable()
    {
        var manager = new PowerShell();
        var packages = PowerShell.ParseInstalledPackages(
            [
                "Version Name Repository Description",
                "------- ---- ---------- -----------",
                "5.5.0 Pester PSGallery Test framework",
                "2.2.5 PSReadLine PSGallery Command line editing",
            ],
            manager
        );

        Assert.Collection(
            packages,
            package =>
            {
                Assert.Equal("Pester", package.Id);
                Assert.Equal("5.5.0", package.VersionString);
                Assert.Equal("PSGallery", package.Source.Name);
            },
            package =>
            {
                Assert.Equal("PSReadLine", package.Id);
                Assert.Equal("2.2.5", package.VersionString);
                Assert.Equal("PSGallery", package.Source.Name);
            }
        );
    }

    [Fact]
    public void ParseInstalledPackages_KeepsMultipleInstalledVersions()
    {
        var manager = new PowerShell();

        var packages = PowerShell.ParseInstalledPackages(
            [
                "Version Name Repository Description",
                "------- ---- ---------- -----------",
                "5.5.0 Pester PSGallery Test framework",
                "5.4.0 Pester PSGallery Test framework",
            ],
            manager
        );

        Assert.Equal(["5.5.0", "5.4.0"], packages.Select(package => package.VersionString));
    }

    [Fact]
    public void ParseInstalledPackages_SkipsMalformedLines()
    {
        var manager = new PowerShell();

        var package = Assert.Single(
            PowerShell.ParseInstalledPackages(
                [
                    "Version Name Repository Description",
                    "------- ---- ---------- -----------",
                    "not-enough-columns",
                    "5.5.0 Pester PSGallery Test framework",
                ],
                manager
            )
        );

        Assert.Equal("Pester", package.Id);
    }

    [Fact]
    public void ParseInstalledPackages_PowerShell7TracksScopeAndMultipleVersions()
    {
        var manager = new PowerShell7();

        var packages = PowerShell7.ParseInstalledPackages(
            [
                "##SCOPE:AllUsers##",
                "Name Version Repository",
                "---- ------- ----------",
                "Pester 5.5.0 PSGallery",
                "Pester 5.4.0 PSGallery",
                "##SCOPE:CurrentUser##",
                "Name Version Repository",
                "---- ------- ----------",
                "PSReadLine 2.2.5 PSGallery",
            ],
            manager
        );

        Assert.Collection(
            packages,
            package =>
            {
                Assert.Equal("Pester", package.Id);
                Assert.Equal("5.5.0", package.VersionString);
                Assert.Equal(PackageScope.Machine, package.OverridenOptions.Scope);
            },
            package =>
            {
                Assert.Equal("Pester", package.Id);
                Assert.Equal("5.4.0", package.VersionString);
                Assert.Equal(PackageScope.Machine, package.OverridenOptions.Scope);
            },
            package =>
            {
                Assert.Equal("PSReadLine", package.Id);
                Assert.Equal("2.2.5", package.VersionString);
                Assert.Equal(PackageScope.User, package.OverridenOptions.Scope);
            }
        );
    }

    [Theory]
    [InlineData(PackageScope.Machine, "AllUsers")]
    [InlineData(PackageScope.User, "CurrentUser")]
    public void PowerShell7UninstallParametersIncludeInstalledScope(string packageScope, string psScope)
    {
        var manager = new PowerShell7();
        var package = new PackageBuilder()
            .WithManager(manager)
            .WithId("Pester")
            .WithVersion("5.5.0")
            .WithOptions(new OverridenInstallationOptions(scope: packageScope))
            .Build();

        var parameters = manager.OperationHelper.GetParameters(
            package,
            new InstallOptions(),
            OperationType.Uninstall
        );

        OperationAssert.HasParameters(
            parameters,
            "Uninstall-PSResource",
            "-Name",
            "Pester",
            "-Confirm:$false",
            "-Version",
            "5.5.0",
            "-Scope",
            psScope
        );
    }
}
#endif
