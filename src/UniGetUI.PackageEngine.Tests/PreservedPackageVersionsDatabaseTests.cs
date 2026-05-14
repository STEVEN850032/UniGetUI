using UniGetUI.Core.Data;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.PackageEngine.Classes.Packages.Classes;
using UniGetUI.PackageEngine.Tests.Infrastructure.Builders;

namespace UniGetUI.PackageEngine.Tests;

public sealed class PreservedPackageVersionsDatabaseTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        nameof(PreservedPackageVersionsDatabaseTests),
        Guid.NewGuid().ToString("N")
    );

    public PreservedPackageVersionsDatabaseTests()
    {
        Directory.CreateDirectory(_testRoot);
        CoreData.TEST_DataDirectoryOverride = Path.Combine(_testRoot, "Data");
        Directory.CreateDirectory(CoreData.UniGetUIUserConfigurationDirectory);
        Settings.ResetSettings();
    }

    public void Dispose()
    {
        Settings.ResetSettings();
        CoreData.TEST_DataDirectoryOverride = null;
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void AddGetAndRemovePreservedVersions()
    {
        var manager = new PackageManagerBuilder().WithName("PowerShell7").Build();
        var packageV1 = new PackageBuilder()
            .WithManager(manager)
            .WithId("Contoso.PowerShell")
            .WithVersion("1.0.0")
            .Build();
        var packageV2 = new PackageBuilder()
            .WithManager(manager)
            .WithId("Contoso.PowerShell")
            .WithVersion("2.0.0")
            .Build();

        PreservedPackageVersionsDatabase.Add(packageV1);
        PreservedPackageVersionsDatabase.Add(packageV2);
        PreservedPackageVersionsDatabase.Add(packageV2);

        Assert.True(PreservedPackageVersionsDatabase.IsVersionPreserved(packageV1));
        Assert.True(PreservedPackageVersionsDatabase.IsVersionPreserved(packageV2));
        Assert.Equal(
            ["1.0.0", "2.0.0"],
            PreservedPackageVersionsDatabase.GetPreservedVersions(packageV1)
        );

        Assert.True(PreservedPackageVersionsDatabase.Remove(packageV1));
        Assert.False(PreservedPackageVersionsDatabase.IsVersionPreserved(packageV1));
        Assert.True(PreservedPackageVersionsDatabase.IsVersionPreserved(packageV2));
    }
}
