using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniGetUI.PackageEngine.AgentBroker;

// ═══════════════════════════════════════════════════════════════════════════════
// Request models — matches Rust PackageRequest (deny_unknown_fields)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Package operation request matching the broker protocol v1.0.
/// </summary>
public sealed class BrokerRequest
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://aka.ms/unigetui/package-request.schema.1.0.json";

    [JsonPropertyName("RequestVersion")]
    public string RequestVersion { get; set; } = "1.0.0";

    [JsonPropertyName("RequestType")]
    public string RequestType { get; set; } = "PackageOperation";

    [JsonPropertyName("RequestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("CreatedAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("Operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("Manager")]
    public BrokerRequestManager Manager { get; set; } = new();

    [JsonPropertyName("Source")]
    public BrokerRequestSource Source { get; set; } = new();

    [JsonPropertyName("Package")]
    public BrokerRequestPackage Package { get; set; } = new();

    [JsonPropertyName("Options")]
    public BrokerRequestOptions Options { get; set; } = new();

    [JsonPropertyName("Broker")]
    public BrokerRequestContext Broker { get; set; } = new();
}

public sealed class BrokerRequestManager
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("ExecutableFriendlyName")]
    public string ExecutableFriendlyName { get; set; } = "";
}

public sealed class BrokerRequestSource
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Url")]
    public string? Url { get; set; }

    [JsonPropertyName("IsVirtualManager")]
    public bool? IsVirtualManager { get; set; }
}

/// <summary>
/// Package identification — architecture and version live HERE (not in Options).
/// </summary>
public sealed class BrokerRequestPackage
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("Architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("Channel")]
    public string? Channel { get; set; }
}

/// <summary>
/// Operation options — NO architecture/version here (those are on Package).
/// </summary>
public sealed class BrokerRequestOptions
{
    [JsonPropertyName("Scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("Interactive")]
    public bool Interactive { get; set; }

    [JsonPropertyName("SkipHashCheck")]
    public bool SkipHashCheck { get; set; }

    [JsonPropertyName("PreRelease")]
    public bool PreRelease { get; set; }

    [JsonPropertyName("UninstallPrevious")]
    public bool UninstallPrevious { get; set; }

    [JsonPropertyName("NoUpgrade")]
    public bool NoUpgrade { get; set; }

    [JsonPropertyName("CustomParameters")]
    public List<string> CustomParameters { get; set; } = [];

    [JsonPropertyName("CustomInstallLocation")]
    public string? CustomInstallLocation { get; set; }

    [JsonPropertyName("KillBeforeOperation")]
    public List<string> KillBeforeOperation { get; set; } = [];

    [JsonPropertyName("PreOperationCommand")]
    public string? PreOperationCommand { get; set; }

    [JsonPropertyName("PostOperationCommand")]
    public string? PostOperationCommand { get; set; }
}

public sealed class BrokerRequestContext
{
    [JsonPropertyName("RequestedElevation")]
    public string RequestedElevation { get; set; } = "Elevated";

    [JsonPropertyName("EffectiveUser")]
    public string EffectiveUser { get; set; } = "";

    [JsonPropertyName("ClientVersion")]
    public string? ClientVersion { get; set; }

    [JsonPropertyName("ClientProcessPath")]
    public string? ClientProcessPath { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Response models — matches Rust BrokerResponse
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Broker evaluation/execution response.
/// </summary>
public sealed class BrokerResponse
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("ResponseVersion")]
    public string ResponseVersion { get; set; } = "";

    [JsonPropertyName("ResponseType")]
    public string ResponseType { get; set; } = "";

    [JsonPropertyName("Broker")]
    public BrokerResponseInfo? Broker { get; set; }

    [JsonPropertyName("AuditId")]
    public string AuditId { get; set; } = "";

    [JsonPropertyName("RequestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("ReceivedAt")]
    public string ReceivedAt { get; set; } = "";

    [JsonPropertyName("CompletedAt")]
    public string CompletedAt { get; set; } = "";

    [JsonPropertyName("Manager")]
    public string? Manager { get; set; }

    [JsonPropertyName("Source")]
    public string? Source { get; set; }

    [JsonPropertyName("PackageId")]
    public string? PackageId { get; set; }

    [JsonPropertyName("Operation")]
    public string? Operation { get; set; }

    [JsonPropertyName("Decision")]
    public string Decision { get; set; } = "";

    [JsonPropertyName("RuleId")]
    public string RuleId { get; set; } = "";

    [JsonPropertyName("Reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("WouldExecute")]
    public bool WouldExecute { get; set; }

    [JsonPropertyName("Policy")]
    public BrokerPolicyInfo? Policy { get; set; }

    [JsonPropertyName("Execution")]
    public BrokerExecutionInfo? Execution { get; set; }
}

public sealed class BrokerResponseInfo
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("ProtocolVersion")]
    public string ProtocolVersion { get; set; } = "";

    [JsonPropertyName("Transport")]
    public string Transport { get; set; } = "";

    [JsonPropertyName("PipeName")]
    public string? PipeName { get; set; }

    [JsonPropertyName("ElevatedSimulation")]
    public bool ElevatedSimulation { get; set; }
}

public sealed class BrokerPolicyInfo
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("Revision")]
    public int Revision { get; set; }

    [JsonPropertyName("PolicyVersion")]
    public string PolicyVersion { get; set; } = "";
}

public sealed class BrokerExecutionInfo
{
    [JsonPropertyName("Mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("Command")]
    public List<string> Command { get; set; } = [];

    [JsonPropertyName("Note")]
    public string Note { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════════════════════════
// Status query models — matches Rust StatusRequest / StatusResponse
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Status query request for a previously submitted package operation.
/// </summary>
public sealed class BrokerStatusRequest
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://aka.ms/unigetui/package-operation-status-request.schema.1.0.json";

    [JsonPropertyName("RequestVersion")]
    public string RequestVersion { get; set; } = "1.0.0";

    [JsonPropertyName("RequestType")]
    public string RequestType { get; set; } = "PackageOperationStatus";

    [JsonPropertyName("RequestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("Broker")]
    public BrokerRequestContext Broker { get; set; } = new();
}

/// <summary>
/// Status query response from the broker.
/// </summary>
public sealed class BrokerStatusResponse
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("ResponseVersion")]
    public string ResponseVersion { get; set; } = "";

    [JsonPropertyName("ResponseType")]
    public string ResponseType { get; set; } = "";

    [JsonPropertyName("Broker")]
    public BrokerResponseInfo? Broker { get; set; }

    [JsonPropertyName("RequestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("Status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("StartedAt")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("CompletedAt")]
    public string? CompletedAt { get; set; }

    [JsonPropertyName("ExitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("Note")]
    public string? Note { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrokerRequest))]
[JsonSerializable(typeof(BrokerResponse))]
[JsonSerializable(typeof(BrokerStatusRequest))]
[JsonSerializable(typeof(BrokerStatusResponse))]
public sealed partial class BrokerJsonContext : JsonSerializerContext;

