using Devolutions.UniGetUI.Broker.Client;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Serializable;
// Aliased to avoid clashing with UniGetUI.PackageEngine.Enums.Architecture.
using BrokerArchitecture = Devolutions.UniGetUI.Broker.Client.Architecture;

namespace UniGetUI.PackageEngine.AgentBroker;

/// <summary>
/// Builds broker protocol requests from UniGetUI domain objects.
/// Maps IPackage + InstallOptions + OperationType into the canonical
/// <see cref="PackageRequest"/> consumed by the Devolutions Agent broker.
/// </summary>
public static class BrokerRequestBuilder
{
    private static readonly string ClientVersion =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>Build a broker request from UniGetUI package operation parameters.</summary>
    public static PackageRequest Build(IPackage package, InstallOptions options, OperationType role)
    {
        return new PackageRequest
        {
            RequestId = $"req-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.UtcNow,
            Operation = MapOperation(role),
            Manager = new RequestManager
            {
                Name = MapManagerName(package.Manager.Name),
                DisplayName = package.Manager.DisplayName,
                ExecutableFriendlyName = Path.GetFileName(package.Manager.Status.ExecutablePath),
            },
            Source = new RequestSource
            {
                Name = package.Source.Name,
                Url = package.Source.Url?.ToString(),
                IsVirtualManager = false,
            },
            Package = new RequestPackage
            {
                Id = package.Id,
                Name = package.Name,
                Version = string.IsNullOrEmpty(options.Version) ? null : options.Version,
                Architecture = MapArchitecture(options.Architecture),
            },
            Options = new RequestOptions
            {
                Scope = MapScope(options.InstallationScope),
                Interactive = options.InteractiveInstallation,
                SkipHashCheck = options.SkipHashCheck,
                PreRelease = options.PreRelease,
                CustomParameters = GetCustomParameters(options, role),
                CustomInstallLocation = string.IsNullOrEmpty(options.CustomInstallLocation) ? null : options.CustomInstallLocation,
                KillBeforeOperation = options.KillBeforeOperation ?? [],
                PreOperationCommand = GetPreCommand(options, role),
                PostOperationCommand = GetPostCommand(options, role),
            },
            Broker = new BrokerContext
            {
                RequestedElevation = options.RunAsAdministrator ? Elevation.Elevated : Elevation.Standard,
                EffectiveUser = $"{Environment.UserDomainName}\\{Environment.UserName}",
                ClientVersion = ClientVersion,
                ClientProcessPath = Environment.ProcessPath,
            },
        };
    }

    private static Operation MapOperation(OperationType role) => role switch
    {
        OperationType.Install => Operation.Install,
        OperationType.Update => Operation.Update,
        OperationType.Uninstall => Operation.Uninstall,
        _ => throw new ArgumentException($"Unsupported operation type: {role}"),
    };

    /// <summary>
    /// Maps UniGetUI manager names to the broker protocol canonical managers.
    /// PowerShell 5 and PowerShell 7 are modeled as separate managers.
    /// </summary>
    private static ManagerName MapManagerName(string managerName)
    {
        if (managerName.Equals("Winget", StringComparison.OrdinalIgnoreCase))
        {
            return ManagerName.Winget;
        }

        if (managerName.Equals("PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            return ManagerName.PowerShell;
        }

        if (managerName.Equals("PowerShell7", StringComparison.OrdinalIgnoreCase) ||
            managerName.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            return ManagerName.PowerShell7;
        }

        throw new ArgumentException($"Unsupported manager for the broker: {managerName}");
    }

    private static Scope? MapScope(string? scope)
    {
        if (string.IsNullOrEmpty(scope))
        {
            return null;
        }

        return scope.ToLowerInvariant() switch
        {
            "user" => Scope.User,
            "machine" => Scope.Machine,
            "global" => Scope.Machine,
            _ => null,
        };
    }

    private static BrokerArchitecture? MapArchitecture(string? architecture)
    {
        if (string.IsNullOrEmpty(architecture))
        {
            return null;
        }

        return architecture.ToLowerInvariant() switch
        {
            "x86" => BrokerArchitecture.X86,
            "x64" => BrokerArchitecture.X64,
            "arm64" => BrokerArchitecture.Arm64,
            "neutral" => BrokerArchitecture.Neutral,
            _ => null,
        };
    }

    private static List<string> GetCustomParameters(InstallOptions options, OperationType role) => role switch
    {
        OperationType.Install => options.CustomParameters_Install ?? [],
        OperationType.Update => options.CustomParameters_Update ?? [],
        OperationType.Uninstall => options.CustomParameters_Uninstall ?? [],
        _ => [],
    };

    private static string? GetPreCommand(InstallOptions options, OperationType role) => role switch
    {
        OperationType.Install => NullIfEmpty(options.PreInstallCommand),
        OperationType.Update => NullIfEmpty(options.PreUpdateCommand),
        OperationType.Uninstall => NullIfEmpty(options.PreUninstallCommand),
        _ => null,
    };

    private static string? GetPostCommand(InstallOptions options, OperationType role) => role switch
    {
        OperationType.Install => NullIfEmpty(options.PostInstallCommand),
        OperationType.Update => NullIfEmpty(options.PostUpdateCommand),
        OperationType.Uninstall => NullIfEmpty(options.PostUninstallCommand),
        _ => null,
    };

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
