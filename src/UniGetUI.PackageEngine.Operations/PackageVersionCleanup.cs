using UniGetUI.Core.Classes;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.Interface.Enums;
using UniGetUI.PackageEngine.Classes.Packages.Classes;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Serializable;
using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Operations;

public static class PackageVersionCleanupHelper
{
    public static bool SupportsVersionCleanup(IPackage? package) =>
        package?.Manager.Capabilities.CanUninstallPreviousVersionsAfterUpdate is true;

    public static bool ShouldRunAutomaticCleanup(
        IPackage package,
        InstallOptions options,
        OperationType triggerRole
    )
    {
        return triggerRole is OperationType.Install or OperationType.Update
            && SupportsVersionCleanup(package)
            && options.UninstallPreviousVersionsOnUpdate;
    }

    public static string GetCleanupAnchorVersion(
        IPackage package,
        InstallOptions options,
        OperationType triggerRole
    )
    {
        if (triggerRole is OperationType.Update && !string.IsNullOrWhiteSpace(package.NewVersionString))
        {
            return package.NewVersionString;
        }

        if (triggerRole is OperationType.Install && !string.IsNullOrWhiteSpace(options.Version))
        {
            return options.Version;
        }

        return package.VersionString;
    }

    public static IReadOnlyList<IPackage> GetManualCleanupTargets(
        IPackage selectedPackage,
        IEnumerable<IPackage> installedPackages
    )
    {
        if (!SupportsVersionCleanup(selectedPackage) || !HasKnownVersion(selectedPackage))
        {
            return [];
        }

        var equivalents = installedPackages
            .Where(package => IsCleanupEquivalentTo(package, selectedPackage, options: null))
            .Where(HasKnownVersion)
            .ToArray();
        IPackage? newest = equivalents.OrderByDescending(package => package.NormalizedVersion).FirstOrDefault();

        return equivalents
            .Where(package => !HasSameVersion(package, selectedPackage))
            .Where(package => newest is null || !HasSameVersion(package, newest))
            .Where(package => !PreservedPackageVersionsDatabase.IsVersionPreserved(package))
            .OrderBy(package => package.NormalizedVersion)
            .ToArray();
    }

    public static IReadOnlyList<IPackage> GetAutomaticCleanupTargets(
        IPackage package,
        IEnumerable<IPackage> installedPackages,
        string anchorVersion,
        InstallOptions options
    )
    {
        if (!SupportsVersionCleanup(package))
        {
            return [];
        }

        var normalizedAnchor = CoreTools.VersionStringToStruct(anchorVersion);
        if (normalizedAnchor == CoreTools.Version.Null)
        {
            Logger.Warn(
                $"Skipping old-version cleanup for {package.Id}; cleanup anchor version {anchorVersion} could not be parsed"
            );
            return [];
        }

        return installedPackages
            .Where(candidate => IsCleanupEquivalentTo(candidate, package, options))
            .Where(HasKnownVersion)
            .Where(candidate => candidate.NormalizedVersion < normalizedAnchor)
            .Where(candidate => !PreservedPackageVersionsDatabase.IsVersionPreserved(candidate))
            .OrderBy(candidate => candidate.NormalizedVersion)
            .ToArray();
    }

    public static bool HasKnownVersion(IPackage package) =>
        !string.IsNullOrWhiteSpace(package.VersionString)
        && package.NormalizedVersion != CoreTools.Version.Null;

    private static bool HasSameVersion(IPackage package, IPackage otherPackage) =>
        package.VersionString.Equals(otherPackage.VersionString, StringComparison.OrdinalIgnoreCase);

    private static bool IsCleanupEquivalentTo(
        IPackage candidate,
        IPackage package,
        InstallOptions? options
    )
    {
        if (!candidate.IsEquivalentTo(package))
        {
            return false;
        }

        if (package.Manager.Name is not "PowerShell7")
        {
            return true;
        }

        string? cleanupScope = GetPowerShell7CleanupScope(package, options);
        return cleanupScope is null
            ? candidate.OverridenOptions.Scope is null
            : candidate.OverridenOptions.Scope == cleanupScope;
    }

    private static string? GetPowerShell7CleanupScope(IPackage package, InstallOptions? options)
    {
        if (!string.IsNullOrWhiteSpace(package.OverridenOptions.Scope))
        {
            return package.OverridenOptions.Scope;
        }

        return options?.InstallationScope switch
        {
            PackageScope.Global => PackageScope.Machine,
            _ when options is not null => PackageScope.User,
            _ => null,
        };
    }
}

public sealed class PackageVersionCleanupOperation : AbstractOperation
{
    private readonly IPackage _package;
    private readonly InstallOptions _options;
    private readonly OperationType _triggerRole;

    public PackageVersionCleanupOperation(
        IPackage package,
        InstallOptions options,
        OperationType triggerRole
    )
        : base(queue_enabled: false)
    {
        _package = package;
        _options = options;
        _triggerRole = triggerRole;

        Metadata.OperationInformation =
            "Old-version cleanup for Package="
            + _package.Id
            + " with Manager="
            + _package.Manager.Name
            + "\nTriggered by operation: "
            + _triggerRole
            + "\nInstallation options: "
            + _options;
        Metadata.Title = CoreTools.Translate("{0} cleanup", _package.Name);
        Metadata.Status = CoreTools.Translate(
            "Removing old versions of package {0}",
            _package.Name
        );
        Metadata.SuccessTitle = CoreTools.Translate("Cleanup completed");
        Metadata.SuccessMessage = CoreTools.Translate(
            "Old versions of {0} were processed",
            _package.Name
        );
        Metadata.FailureTitle = CoreTools.Translate("Cleanup failed");
        Metadata.FailureMessage = CoreTools.Translate(
            "Old versions of {0} could not be removed",
            _package.Name
        );
    }

    protected override void ApplyRetryAction(string retryMode) { }

    protected override async Task<OperationVeredict> PerformOperation()
    {
        if (!PackageVersionCleanupHelper.ShouldRunAutomaticCleanup(_package, _options, _triggerRole))
        {
            Line(CoreTools.Translate("Old-version cleanup is disabled"), LineType.Information);
            return OperationVeredict.Success;
        }

        string anchorVersion = PackageVersionCleanupHelper.GetCleanupAnchorVersion(
            _package,
            _options,
            _triggerRole
        );
        IReadOnlyList<IPackage> installedPackages;
        try
        {
            installedPackages = await Task.Run(_package.Manager.GetInstalledPackages);
        }
        catch (Exception ex)
        {
            Logger.Warn(
                $"Could not refresh installed package versions for cleanup of {_package.Id}"
            );
            Logger.Warn(ex);
            return OperationVeredict.Success;
        }

        var cleanupTargets = PackageVersionCleanupHelper.GetAutomaticCleanupTargets(
            _package,
            installedPackages,
            anchorVersion,
            _options
        );
        if (cleanupTargets.Count == 0)
        {
            Line(CoreTools.Translate("No old package versions need cleanup"), LineType.Information);
            return OperationVeredict.Success;
        }

        foreach (var cleanupTarget in cleanupTargets)
        {
            Logger.Info(
                $"Queuing old-version cleanup for {cleanupTarget.Id} version {cleanupTarget.VersionString}"
            );
            var uninstallOperation = new UninstallPackageOperation(
                cleanupTarget,
                _options.Copy(),
                IgnoreParallelInstalls: true
            );
            uninstallOperation.LogLineAdded += (_, line) => Line(line.Item1, line.Item2);
            var innerOperation = new InnerOperation(uninstallOperation, mustSucceed: false);
            await innerOperation.Operation.MainThread();
        }

        return OperationVeredict.Success;
    }

    public override Task<Uri> GetOperationIcon()
    {
        return TaskRecycler<Uri>.RunOrAttachAsync(_package.GetIconUrl);
    }
}
