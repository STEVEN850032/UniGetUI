using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniGetUI.PackageEngine.AgentBroker;

/// <summary>
/// Request models matching the UniGetUI package broker protocol v1.0.
/// These correspond to schemas/unigetui.package-request.schema.1.0.json.
/// </summary>
public sealed class BrokerRequest
{
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
    public bool IsVirtualManager { get; set; }
}

public sealed class BrokerRequestPackage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("newVersion")]
    public string? NewVersion { get; set; }
}

public sealed class BrokerRequestOptions
{
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    [JsonPropertyName("interactive")]
    public bool Interactive { get; set; }

    [JsonPropertyName("runAsAdministrator")]
    public bool RunAsAdministrator { get; set; }

    [JsonPropertyName("skipHashCheck")]
    public bool SkipHashCheck { get; set; }

    [JsonPropertyName("preRelease")]
    public bool PreRelease { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

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
    public string ClientVersion { get; set; } = "";
}

/// <summary>
/// Response models matching the UniGetUI package broker protocol v1.0.
/// These correspond to schemas/unigetui.package-broker-response.schema.1.0.json.
/// </summary>
public sealed class BrokerResponse
{
    [JsonPropertyName("responseVersion")]
    public string ResponseVersion { get; set; } = "";

    [JsonPropertyName("responseType")]
    public string ResponseType { get; set; } = "";

    [JsonPropertyName("broker")]
    public BrokerResponseInfo? Broker { get; set; }

    [JsonPropertyName("auditId")]
    public string AuditId { get; set; } = "";

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("receivedAt")]
    public string? ReceivedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; set; }

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
    public string PipeName { get; set; } = "";

    [JsonPropertyName("elevatedSimulation")]
    public bool ElevatedSimulation { get; set; }
}

public sealed class BrokerPolicyInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("defaultDecision")]
    public string DefaultDecision { get; set; } = "";
}

public sealed class BrokerExecutionInfo
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("command")]
    public List<string> Command { get; set; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BrokerRequest))]
[JsonSerializable(typeof(BrokerResponse))]
internal sealed partial class BrokerJsonContext : JsonSerializerContext;
