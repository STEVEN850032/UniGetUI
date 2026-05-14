using UniGetUI.Core.Logging;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.PackageEngine.Classes.Packages.Classes;

public static class PreservedPackageVersionsDatabase
{
    private const char VersionSeparator = '|';

    public static IReadOnlyDictionary<string, string> GetDatabase()
    {
        return Settings
                .GetDictionary<string, string>(Settings.K.PreservedPackageVersions)
                ?.Where(kvp => kvp.Value is not null)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)
            ?? new Dictionary<string, string>();
    }

    public static string GetPreservedIdForPackage(IPackage package)
    {
        return string.Join(
            "\\",
            package.Manager.Properties.Name.ToLowerInvariant(),
            package.Source.Name.ToLowerInvariant(),
            package.Id
        );
    }

    public static IReadOnlyList<string> GetPreservedVersions(string preservedId)
    {
        string? rawVersions = Settings.GetDictionaryItem<string, string>(
            Settings.K.PreservedPackageVersions,
            preservedId
        );

        if (string.IsNullOrWhiteSpace(rawVersions))
        {
            return [];
        }

        return rawVersions
            .Split(VersionSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetPreservedVersions(IPackage package) =>
        GetPreservedVersions(GetPreservedIdForPackage(package));

    public static bool IsVersionPreserved(IPackage package) =>
        IsVersionPreserved(GetPreservedIdForPackage(package), package.VersionString);

    public static bool IsVersionPreserved(string preservedId, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return GetPreservedVersions(preservedId).Contains(version, StringComparer.OrdinalIgnoreCase);
    }

    public static void Add(IPackage package) =>
        Add(GetPreservedIdForPackage(package), package.VersionString);

    public static void Add(string preservedId, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            Logger.Warn($"Attempted to preserve an empty package version for {preservedId}");
            return;
        }

        var versions = GetPreservedVersions(preservedId).ToList();
        if (!versions.Contains(version, StringComparer.OrdinalIgnoreCase))
        {
            versions.Add(version);
        }

        Save(preservedId, versions);
    }

    public static bool Remove(IPackage package) =>
        Remove(GetPreservedIdForPackage(package), package.VersionString);

    public static bool Remove(string preservedId, string version)
    {
        var versions = GetPreservedVersions(preservedId).ToList();
        bool removed = versions.RemoveAll(v => v.Equals(version, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            Logger.Warn(
                $"Attempted to remove package version {{preservedId={preservedId}; version={version}}} from preserved versions, but it was not found"
            );
            return false;
        }

        Save(preservedId, versions);
        return true;
    }

    private static void Save(string preservedId, IReadOnlyList<string> versions)
    {
        string rawVersions = string.Join(
            VersionSeparator,
            versions
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
        );

        if (rawVersions.Length == 0)
        {
            Settings.RemoveDictionaryKey<string, string>(
                Settings.K.PreservedPackageVersions,
                preservedId
            );
            return;
        }

        Settings.SetDictionaryItem(Settings.K.PreservedPackageVersions, preservedId, rawVersions);
    }
}
