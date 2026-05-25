using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Serializable;

namespace UniGetUI.PackageEngine.AgentBroker;

/// <summary>
/// Builds broker protocol requests from UniGetUI domain objects.
/// Maps IPackage + InstallOptions + OperationType into the canonical
/// package operation request format expected by the Devolutions Agent broker.
/// </summary>
public static class BrokerRequestBuilder
{
    private static readonly string ClientVersion =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// Build a broker request from UniGetUI package operation parameters.
    /// </summary>
    public static BrokerRequest Build(IPackage package, InstallOptions options, OperationType role)
    {
        var request = new BrokerRequest
        {
            RequestId = $"req-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            Operation = MapOperation(role),
            Manager = new BrokerRequestManager
            {
                Name = MapManagerName(package.Manager.Name),
                DisplayName = package.Manager.DisplayName,
                ExecutableFriendlyName = Path.GetFileName(package.Manager.Status.ExecutablePath)
            },
            Source = new BrokerRequestSource
            {
                Name = package.Source.Name,
                Url = package.Source.Url?.ToString(),
                IsVirtualManager = false
            },
            Package = new BrokerRequestPackage
            {
                Id = package.Id,
                Name = package.Name,
                Version = string.IsNullOrEmpty(options.Version) ? null : options.Version,
                Architecture = string.IsNullOrEmpty(options.Architecture) ? null : options.Architecture.ToLowerInvariant(),
            },
            Options = new BrokerRequestOptions
            {
                Scope = MapScope(options.InstallationScope),
                Interactive = options.InteractiveInstallation,
                RunAsAdministrator = options.RunAsAdministrator,
                SkipHashCheck = options.SkipHashCheck,
                PreRelease = options.PreRelease,
                CustomParameters = GetCustomParameters(options, role),
                CustomInstallLocation = string.IsNullOrEmpty(options.CustomInstallLocation) ? null : options.CustomInstallLocation,
                KillBeforeOperation = options.KillBeforeOperation ?? [],
                PreOperationCommand = GetPreCommand(options, role),
                PostOperationCommand = GetPostCommand(options, role)
            },
            Broker = new BrokerRequestContext
            {
                RequestedElevation = options.RunAsAdministrator ? "elevated" : "standard",
                EffectiveUser = $"{Environment.UserDomainName}\\{Environment.UserName}",
                ClientVersion = ClientVersion,
                ClientProcessPath = Environment.ProcessPath
            }
        };

        return request;
    }

    private static string MapOperation(OperationType role) => role switch
    {
        OperationType.Install => "install",
        OperationType.Update => "update",
        OperationType.Uninstall => "uninstall",
        _ => throw new ArgumentException($"Unsupported operation type: {role}")
    };

    /// <summary>
    /// Maps UniGetUI manager names to the broker protocol canonical names.
    /// Only WinGet is supported in this iteration.
    /// </summary>
    private static string MapManagerName(string managerName)
    {
        // UniGetUI uses "Winget" internally for the WinGet manager.
        if (managerName.Equals("Winget", StringComparison.OrdinalIgnoreCase) ||
            managerName.Equals("WinGet", StringComparison.OrdinalIgnoreCase))
        {
            return "Winget";
        }

        // Return as-is for unsupported managers (broker will reject).
        return managerName;
    }

    private static string? MapScope(string? scope)
    {
        if (string.IsNullOrEmpty(scope)) return null;
        return scope.ToLowerInvariant() switch
        {
            "user" => "user",
            "machine" => "machine",
            "global" => "machine",
            _ => scope.ToLowerInvariant()
        };
    }

    private static List<string> GetCustomParameters(InstallOptions options, OperationType role) => role switch
    {
        OperationType.Install => options.CustomParameters_Install ?? [],
        OperationType.Update => options.CustomParameters_Update ?? [],
        OperationType.Uninstall => options.CustomParameters_Uninstall ?? [],
        _ => []
    };

    private static string? GetPreCommand(InstallOptions options, OperationType role) => role switch
    {
        OperationType.Install => NullIfEmpty(options.PreInstallCommand),
        OperationType.Update => NullIfEmpty(options.PreUpdateCommand),
        OperationType.Uninstall => NullIfEmpty(options.PreUninstallCommand),
        _ => null
    };

    private static string? GetPostCommand(InstallOptions options, OperationType role) => role switch
    {
        OperationType.Install => NullIfEmpty(options.PostInstallCommand),
        OperationType.Update => NullIfEmpty(options.PostUpdateCommand),
        OperationType.Uninstall => NullIfEmpty(options.PostUninstallCommand),
        _ => null
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
