using UniGetUI.PackageEngine.PackageLoader;
using UniGetUI.PackageEngine.Tests.Infrastructure.Builders;

namespace UniGetUI.PackageEngine.Tests;

public sealed class PackageListQueryTests
{
    [Fact]
    public void Apply_FiltersByNameOrIdIgnoringCaseAndSpecialCharacters()
    {
        var manager = new PackageManagerBuilder().Build();
        var packages = new[]
        {
            new PackageBuilder().WithManager(manager).WithName("Déjà Tool").WithId("contoso.deja").Build(),
            new PackageBuilder().WithManager(manager).WithName("Other").WithId("example.other").Build(),
        };

        var result = PackageListQuery.Apply(packages, new PackageListQueryOptions
        {
            Query = "deja",
        });

        Assert.Single(result.Packages);
        Assert.Equal("contoso.deja", result.Packages[0].Id);
    }

    [Fact]
    public void Apply_FiltersBySelectedManager()
    {
        var selectedManager = new PackageManagerBuilder()
            .WithName("Selected")
            .WithDisplayName("Selected")
            .Build();
        var hiddenManager = new PackageManagerBuilder()
            .WithName("Hidden")
            .WithDisplayName("Hidden")
            .Build();
        var packages = new[]
        {
            new PackageBuilder().WithManager(selectedManager).WithId("selected").Build(),
            new PackageBuilder().WithManager(hiddenManager).WithId("hidden").Build(),
        };

        var result = PackageListQuery.Apply(packages, new PackageListQueryOptions
        {
            SelectedManagers = new HashSet<Interfaces.IPackageManager> { selectedManager },
        });

        Assert.Single(result.Packages);
        Assert.Equal("selected", result.Packages[0].Id);
    }

    [Fact]
    public void Apply_SortsByIdDescending()
    {
        var manager = new PackageManagerBuilder().Build();
        var packages = new[]
        {
            new PackageBuilder().WithManager(manager).WithId("alpha").Build(),
            new PackageBuilder().WithManager(manager).WithId("zulu").Build(),
        };

        var result = PackageListQuery.Apply(packages, new PackageListQueryOptions
        {
            SortField = PackageListSortField.Id,
            SortAscending = false,
        });

        Assert.Equal(["zulu", "alpha"], result.Packages.Select(package => package.Id));
    }

    [Fact]
    public void BuildSubtitle_IncludesSelectionWhenPresent()
    {
        string subtitle = PackageListQuery.BuildSubtitle(
            visibleCount: 2,
            totalCount: 5,
            selectedCount: 1,
            isLoading: false
        );

        Assert.Contains("2", subtitle);
        Assert.Contains("5", subtitle);
        Assert.Contains("1", subtitle);
    }
}
