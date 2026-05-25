using System.Text.Json;
using UniGetUI.PackageEngine.AgentBroker;

namespace UniGetUI.PackageEngine.AgentBroker.Tests;

/// <summary>
/// Verifies that C# broker models can deserialize all Rust-generated sample files
/// without data loss. This ensures wire compatibility with the Rust broker implementation.
/// </summary>
public class BrokerModelDeserializationTests
{
    private static string SamplesRoot =>
        Path.Combine(AppContext.BaseDirectory, "samples");

    [Theory]
    [MemberData(nameof(GetRequestFiles))]
    public void DeserializeRequest_RustSample(string fileName)
    {
        var path = Path.Combine(SamplesRoot, "requests", fileName);
        var json = File.ReadAllText(path);
        var request = JsonSerializer.Deserialize(json, BrokerJsonContext.Default.BrokerRequest);

        Assert.NotNull(request);
        Assert.False(string.IsNullOrEmpty(request.RequestId), $"requestId must not be empty in {fileName}");
        Assert.False(string.IsNullOrEmpty(request.Operation), $"operation must not be empty in {fileName}");
    }

    [Theory]
    [MemberData(nameof(GetStatusRequestFiles))]
    public void DeserializeStatusRequest_RustSample(string fileName)
    {
        var path = Path.Combine(SamplesRoot, "requests", fileName);
        var json = File.ReadAllText(path);
        var request = JsonSerializer.Deserialize(json, BrokerJsonContext.Default.BrokerStatusRequest);

        Assert.NotNull(request);
        Assert.False(string.IsNullOrEmpty(request.RequestId), $"requestId must not be empty in {fileName}");
    }

    [Theory]
    [MemberData(nameof(GetAllowedResponseFiles))]
    public void DeserializeResponse_RustSample(string fileName)
    {
        var path = Path.Combine(SamplesRoot, "responses", fileName);
        var json = File.ReadAllText(path);
        var response = JsonSerializer.Deserialize(json, BrokerJsonContext.Default.BrokerResponse);

        Assert.NotNull(response);
        Assert.False(string.IsNullOrEmpty(response.RequestId), $"requestId must not be empty in {fileName}");
        Assert.False(string.IsNullOrEmpty(response.Decision), $"decision must not be empty in {fileName}");
    }

    [Theory]
    [MemberData(nameof(GetStatusResponseFiles))]
    public void DeserializeStatusResponse_RustSample(string fileName)
    {
        var path = Path.Combine(SamplesRoot, "responses", fileName);
        var json = File.ReadAllText(path);
        var response = JsonSerializer.Deserialize(json, BrokerJsonContext.Default.BrokerStatusResponse);

        Assert.NotNull(response);
        Assert.False(string.IsNullOrEmpty(response.RequestId), $"requestId must not be empty in {fileName}");
        Assert.False(string.IsNullOrEmpty(response.Status), $"status must not be empty in {fileName}");
    }

    [Fact]
    public void SerializeRequest_RoundTrip_PreservesFields()
    {
        var path = Path.Combine(SamplesRoot, "requests", "winget-vscode-install.request.json");
        var json = File.ReadAllText(path);
        var request = JsonSerializer.Deserialize(json, BrokerJsonContext.Default.BrokerRequest)!;

        // Verify key fields survived deserialization
        Assert.Equal("req-winget-vscode-install", request.RequestId);
        Assert.Equal("install", request.Operation);
        Assert.Equal("Winget", request.Manager.Name);
        Assert.Equal("winget", request.Source.Name);
        Assert.Equal("Microsoft.VisualStudioCode", request.Package.Id);
        Assert.Equal("x64", request.Package.Architecture);
        Assert.Equal("machine", request.Options.Scope);
        Assert.True(request.Options.RunAsAdministrator);
        Assert.Equal("elevated", request.Broker.RequestedElevation);
        Assert.Equal("CONTOSO\\alice", request.Broker.EffectiveUser);

        // Round-trip: serialize back and re-parse
        var reserialized = JsonSerializer.Serialize(request, BrokerJsonContext.Default.BrokerRequest);
        var reparsed = JsonSerializer.Deserialize(reserialized, BrokerJsonContext.Default.BrokerRequest)!;
        Assert.Equal(request.RequestId, reparsed.RequestId);
        Assert.Equal(request.Package.Architecture, reparsed.Package.Architecture);
    }

    [Fact]
    public void SerializeStatusResponse_RoundTrip_PreservesFields()
    {
        var path = Path.Combine(SamplesRoot, "responses", "status-completed.response.json");
        var json = File.ReadAllText(path);
        var response = JsonSerializer.Deserialize(json, BrokerJsonContext.Default.BrokerStatusResponse)!;

        Assert.Equal("req-winget-vscode-install", response.RequestId);
        Assert.Equal("completed", response.Status);
        Assert.Equal(0, response.ExitCode);
        Assert.Equal("Process exited successfully.", response.Note);
    }

    public static TheoryData<string> GetRequestFiles()
    {
        var data = new TheoryData<string>();
        var dir = Path.Combine(AppContext.BaseDirectory, "samples", "requests");
        foreach (var file in Directory.EnumerateFiles(dir, "*.request.json"))
        {
            var name = Path.GetFileName(file);
            // Skip status requests — they use a different schema
            if (!name.Contains("status"))
                data.Add(name);
        }
        return data;
    }

    public static TheoryData<string> GetStatusRequestFiles()
    {
        var data = new TheoryData<string>();
        var dir = Path.Combine(AppContext.BaseDirectory, "samples", "requests");
        foreach (var file in Directory.EnumerateFiles(dir, "*status*.request.json"))
        {
            data.Add(Path.GetFileName(file));
        }
        return data;
    }

    public static TheoryData<string> GetAllowedResponseFiles()
    {
        var data = new TheoryData<string>();
        var dir = Path.Combine(AppContext.BaseDirectory, "samples", "responses");
        foreach (var file in Directory.EnumerateFiles(dir, "*.response.json"))
        {
            var name = Path.GetFileName(file);
            // Only non-status responses use BrokerResponse
            if (!name.StartsWith("status-"))
                data.Add(name);
        }
        return data;
    }

    public static TheoryData<string> GetStatusResponseFiles()
    {
        var data = new TheoryData<string>();
        var dir = Path.Combine(AppContext.BaseDirectory, "samples", "responses");
        foreach (var file in Directory.EnumerateFiles(dir, "status-*.response.json"))
        {
            data.Add(Path.GetFileName(file));
        }
        return data;
    }
}
