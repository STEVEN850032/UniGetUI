using System.Globalization;
using System.Text;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.PackageEngine.PackageLoader;

public enum PackageListSearchMode
{
    Both,
    Name,
    Id,
    Exact,
}

public enum PackageListSortField
{
    Name,
    Id,
    Version,
    NewVersion,
    Source,
    Manager,
}

public sealed class PackageListQueryOptions
{
    public string Query { get; set; } = "";
    public PackageListSearchMode SearchMode { get; set; } = PackageListSearchMode.Both;
    public PackageListSortField SortField { get; set; } = PackageListSortField.Name;
    public bool SortAscending { get; set; } = true;
    public bool MatchCase { get; set; }
    public bool IgnoreSpecialCharacters { get; set; } = true;
    public IReadOnlySet<IManagerSource> SelectedSources { get; set; } =
        new HashSet<IManagerSource>();
    public IReadOnlySet<IPackageManager> SelectedManagers { get; set; } =
        new HashSet<IPackageManager>();
}

public sealed class PackageListQueryResult
{
    public PackageListQueryResult(
        IReadOnlyList<IPackage> packages,
        int totalCount,
        int selectedCount,
        IReadOnlyList<IPackageManager> managers,
        IReadOnlyList<IManagerSource> sources
    )
    {
        Packages = packages;
        TotalCount = totalCount;
        SelectedCount = selectedCount;
        Managers = managers;
        Sources = sources;
    }

    public IReadOnlyList<IPackage> Packages { get; }
    public int TotalCount { get; }
    public int SelectedCount { get; }
    public IReadOnlyList<IPackageManager> Managers { get; }
    public IReadOnlyList<IManagerSource> Sources { get; }
}

public static class PackageListQuery
{
    public static PackageListQueryResult Apply(
        IEnumerable<IPackage> packages,
        PackageListQueryOptions options
    )
    {
        var packageList = packages.ToList();
        string query = Normalize(options.Query, options);

        IEnumerable<IPackage> filtered = packageList.Where(package =>
            MatchesQuery(package, query, options)
            && MatchesSource(package, options)
        );

        filtered = ApplySorting(filtered, options);
        var result = filtered.ToList();

        return new PackageListQueryResult(
            result,
            packageList.Count,
            result.Count(package => package.IsChecked),
            packageList.Select(package => package.Manager).Distinct().ToList(),
            packageList.Select(package => package.Source).Distinct().ToList()
        );
    }

    public static IReadOnlyList<IPackage> GetCheckedPackages(IEnumerable<IPackage> packages) =>
        packages.Where(package => package.IsChecked).ToList();

    public static void SetChecked(IEnumerable<IPackage> packages, bool isChecked)
    {
        foreach (IPackage package in packages)
        {
            package.IsChecked = isChecked;
        }
    }

    public static string BuildSubtitle(
        int visibleCount,
        int totalCount,
        int selectedCount,
        bool isLoading
    )
    {
        if (isLoading)
            return CoreTools.Translate("Loading packages");

        string countText = visibleCount == totalCount
            ? CoreTools.Translate("{0} packages found", totalCount)
            : CoreTools.Translate("{0} of {1} packages shown", visibleCount, totalCount);

        if (selectedCount == 0)
            return countText;

        return CoreTools.Translate("{0}, {1} selected", countText, selectedCount);
    }

    private static bool MatchesQuery(
        IPackage package,
        string query,
        PackageListQueryOptions options
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string name = Normalize(package.Name, options);
        string id = Normalize(package.Id, options);

        return options.SearchMode switch
        {
            PackageListSearchMode.Name => name.Contains(query),
            PackageListSearchMode.Id => id.Contains(query),
            PackageListSearchMode.Exact => name == query || id == query,
            _ => name.Contains(query) || id.Contains(query),
        };
    }

    private static bool MatchesSource(IPackage package, PackageListQueryOptions options)
    {
        bool sourceSelected = options.SelectedSources.Count == 0
            || options.SelectedSources.Contains(package.Source);
        bool managerSelected = options.SelectedManagers.Count == 0
            || options.SelectedManagers.Contains(package.Manager);
        return sourceSelected && managerSelected;
    }

    private static IOrderedEnumerable<IPackage> ApplySorting(
        IEnumerable<IPackage> packages,
        PackageListQueryOptions options
    )
    {
        Func<IPackage, string> selector = options.SortField switch
        {
            PackageListSortField.Id => package => package.Id,
            PackageListSortField.Version => package => package.NormalizedVersion.ToString(),
            PackageListSortField.NewVersion => package => package.NormalizedNewVersion.ToString(),
            PackageListSortField.Source => package => package.Source.AsString_DisplayName,
            PackageListSortField.Manager => package => package.Manager.DisplayName,
            _ => package => package.Name,
        };

        return options.SortAscending
            ? packages.OrderBy(selector, StringComparer.CurrentCultureIgnoreCase)
            : packages.OrderByDescending(selector, StringComparer.CurrentCultureIgnoreCase);
    }

    private static string Normalize(string value, PackageListQueryOptions options)
    {
        string result = options.MatchCase
            ? value
            : value.ToLower(CultureInfo.CurrentCulture);

        if (!options.IgnoreSpecialCharacters)
            return result;

        var builder = new StringBuilder(result.Length);
        foreach (char c in result.Normalize(NormalizationForm.FormD))
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is not UnicodeCategory.NonSpacingMark
                && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)))
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
