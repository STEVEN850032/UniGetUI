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

    [JsonPropertyName("requestVersion")]
    public string RequestVersion { get; set; } = "1.0.0";

    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = "packageOperation";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("manager")]
    public BrokerRequestManager Manager { get; set; } = new();

    [JsonPropertyName("source")]
    public BrokerRequestSource Source { get; set; } = new();

    [JsonPropertyName("package")]
    public BrokerRequestPackage Package { get; set; } = new();

    [JsonPropertyName("options")]
    public BrokerRequestOptions Options { get; set; } = new();

    [JsonPropertyName("broker")]
    public BrokerRequestContext Broker { get; set; } = new();
}

public sealed class BrokerRequestManager
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("executableFriendlyName")]
    public string ExecutableFriendlyName { get; set; } = "";
}

public sealed class BrokerRequestSource
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("isVirtualManager")]
    public bool? IsVirtualManager { get; set; }
}

/// <summary>
/// Package identification — architecture and version live HERE (not in Options).
/// </summary>
public sealed class BrokerRequestPackage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }
}

/// <summary>
/// Operation options — NO architecture/version here (those are on Package).
/// </summary>
public sealed class BrokerRequestOptions
{
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("interactive")]
    public bool Interactive { get; set; }

    [JsonPropertyName("skipHashCheck")]
    public bool SkipHashCheck { get; set; }

    [JsonPropertyName("preRelease")]
    public bool PreRelease { get; set; }

    [JsonPropertyName("uninstallPrevious")]
    public bool UninstallPrevious { get; set; }

    [JsonPropertyName("noUpgrade")]
    public bool NoUpgrade { get; set; }

    [JsonPropertyName("customParameters")]
    public List<string> CustomParameters { get; set; } = [];

    [JsonPropertyName("customInstallLocation")]
    public string? CustomInstallLocation { get; set; }

    [JsonPropertyName("killBeforeOperation")]
    public List<string> KillBeforeOperation { get; set; } = [];

    [JsonPropertyName("preOperationCommand")]
    public string? PreOperationCommand { get; set; }

    [JsonPropertyName("postOperationCommand")]
    public string? PostOperationCommand { get; set; }
}

public sealed class BrokerRequestContext
{
    [JsonPropertyName("requestedElevation")]
    public string RequestedElevation { get; set; } = "elevated";

    [JsonPropertyName("effectiveUser")]
    public string EffectiveUser { get; set; } = "";

    [JsonPropertyName("clientVersion")]
    public string? ClientVersion { get; set; }

    [JsonPropertyName("clientProcessPath")]
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

    [JsonPropertyName("responseVersion")]
    public string ResponseVersion { get; set; } = "";

    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; } = "";

    [JsonPropertyName("broker")]
    public BrokerResponseInfo? Broker { get; set; }

    [JsonPropertyName("auditId")]
    public string AuditId { get; set; } = "";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("receivedAt")]
    public string ReceivedAt { get; set; } = "";

    [JsonPropertyName("completedAt")]
    public string CompletedAt { get; set; } = "";

    [JsonPropertyName("manager")]
    public string? Manager { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("packageId")]
    public string? PackageId { get; set; }

    [JsonPropertyName("operation")]
    public string? Operation { get; set; }

    [JsonPropertyName("decision")]
    public string Decision { get; set; } = "";

    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("wouldExecute")]
    public bool WouldExecute { get; set; }

    [JsonPropertyName("policy")]
    public BrokerPolicyInfo? Policy { get; set; }

    [JsonPropertyName("execution")]
    public BrokerExecutionInfo? Execution { get; set; }
}

public sealed class BrokerResponseInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "";

    [JsonPropertyName("transport")]
    public string Transport { get; set; } = "";

    [JsonPropertyName("pipeName")]
    public string? PipeName { get; set; }

    [JsonPropertyName("elevatedSimulation")]
    public bool ElevatedSimulation { get; set; }
}

public sealed class BrokerPolicyInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("policyVersion")]
    public string PolicyVersion { get; set; } = "";
}

public sealed class BrokerExecutionInfo
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("command")]
    public List<string> Command { get; set; } = [];

    [JsonPropertyName("note")]
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

    [JsonPropertyName("requestVersion")]
    public string RequestVersion { get; set; } = "1.0.0";

    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = "packageOperationStatus";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("broker")]
    public BrokerRequestContext Broker { get; set; } = new();
}

/// <summary>
/// Status query response from the broker.
/// </summary>
public sealed class BrokerStatusResponse
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("responseVersion")]
    public string ResponseVersion { get; set; } = "";

    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; } = "";

    [JsonPropertyName("broker")]
    public BrokerResponseInfo? Broker { get; set; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }

    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    [JsonPropertyName("note")]
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
