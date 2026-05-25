using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniGetUI.Core.Logging;

namespace UniGetUI.PackageEngine.AgentBroker;

/// <summary>
/// Client for communicating with the Devolutions Agent UniGetUI Package Broker
/// over a Windows named pipe using HTTP/1.1 wire protocol.
/// </summary>
public sealed class BrokerClient : IDisposable
{
    private const string DEFAULT_PIPE_NAME = "UniGetUI.PackageBroker.v1";
    private const string PROTOCOL_VERSION = "1.0";
    private const string REQUEST_MEDIA_TYPE = "application/vnd.unigetui.package-request+json; version=1.0";
    private const string RESPONSE_MEDIA_TYPE = "application/vnd.unigetui.package-broker-response+json; version=1.0";
    private const int CONNECT_TIMEOUT_MS = 5000;
    private const int READ_TIMEOUT_MS = 30000;

    private readonly string _pipeName;

    public BrokerClient(string? pipeName = null)
    {
        _pipeName = pipeName ?? DEFAULT_PIPE_NAME;
    }

    /// <summary>
    /// Check if the broker service is available (pipe exists and responds to health check).
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
#if !WINDOWS
        return false;
#else
        try
        {
            Logger.Debug($"[BrokerClient] Checking availability on pipe '{_pipeName}'...");
            var response = await SendHttpRequestAsync("GET", "/v1/health", null);
            Logger.Debug($"[BrokerClient] Health check: status={response.StatusCode}, body={response.Body}");
            return response.StatusCode == 200;
        }
        catch (Exception ex)
        {
            Logger.Debug($"[BrokerClient] Broker not available: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
#endif
    }

    /// <summary>
    /// Send a package operation request to the broker for evaluation only (dry-run).
    /// </summary>
    public async Task<BrokerResponse?> EvaluateAsync(BrokerRequest request)
    {
#if !WINDOWS
        return null;
#else
        return await SendPackageOperationAsync(request, "/v1/package-operations/evaluate");
#endif
    }

    /// <summary>
    /// Send a package operation request to the broker for execution.
    /// </summary>
    public async Task<BrokerResponse?> ExecuteAsync(BrokerRequest request)
    {
#if !WINDOWS
        return null;
#else
        return await SendPackageOperationAsync(request, "/v1/package-operations");
#endif
    }

    public void Dispose()
    {
        // No persistent resources to dispose.
    }

#if WINDOWS
    private async Task<BrokerResponse?> SendPackageOperationAsync(BrokerRequest request, string endpoint)
    {
        try
        {
            var body = JsonSerializer.Serialize(request, BrokerJsonContext.Default.BrokerRequest);

            Logger.Debug($"[BrokerClient] Sending POST {endpoint} (body length={body.Length})");
            Logger.Debug($"[BrokerClient] Request body: {body}");

            var headers = new Dictionary<string, string>
            {
                ["Content-Type"] = REQUEST_MEDIA_TYPE,
                ["Accept"] = RESPONSE_MEDIA_TYPE,
                ["UniGetUI-Protocol-Version"] = PROTOCOL_VERSION,
                ["UniGetUI-Request-Id"] = request.RequestId,
                ["Host"] = "unigetui-broker"
            };

            var response = await SendHttpRequestAsync("POST", endpoint, body, headers);

            Logger.Debug($"[BrokerClient] Response status={response.StatusCode}, body length={response.Body?.Length ?? 0}");
            Logger.Debug($"[BrokerClient] Response body: {response.Body}");

            if (string.IsNullOrWhiteSpace(response.Body))
            {
                Logger.Error($"[BrokerClient] Empty response body from broker (status: {response.StatusCode})");
                return null;
            }

            var brokerResponse = JsonSerializer.Deserialize(response.Body, BrokerJsonContext.Default.BrokerResponse);
            Logger.Debug($"[BrokerClient] Deserialized: decision={brokerResponse?.Decision}, reason={brokerResponse?.Reason}");
            return brokerResponse;
        }
        catch (Exception ex)
        {
            Logger.Error($"[BrokerClient] Error communicating with broker: {ex.Message}");
            Logger.Error(ex);
            return null;
        }
    }

    /// <summary>
    /// Send a raw HTTP/1.1 request over the named pipe and read the response.
    /// </summary>
    private async Task<HttpPipeResponse> SendHttpRequestAsync(
        string method,
        string path,
        string? body,
        Dictionary<string, string>? extraHeaders = null)
    {
        using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        Logger.Debug($"[BrokerClient] Connecting to pipe '{_pipeName}'...");
        var connectCts = new CancellationTokenSource(CONNECT_TIMEOUT_MS);
        await pipe.ConnectAsync(connectCts.Token);
        Logger.Debug($"[BrokerClient] Connected! IsConnected={pipe.IsConnected}, CanRead={pipe.CanRead}, CanWrite={pipe.CanWrite}");

        // Build HTTP/1.1 request.
        var requestBuilder = new StringBuilder();
        requestBuilder.Append($"{method} {path} HTTP/1.1\r\n");
        requestBuilder.Append("Host: unigetui-broker\r\n");
        requestBuilder.Append("Connection: close\r\n");

        if (extraHeaders != null)
        {
            foreach (var (key, value) in extraHeaders)
            {
                if (!key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    requestBuilder.Append($"{key}: {value}\r\n");
                }
            }
        }

        byte[]? bodyBytes = null;
        if (body != null)
        {
            bodyBytes = Encoding.UTF8.GetBytes(body);
            requestBuilder.Append($"Content-Length: {bodyBytes.Length}\r\n");
        }
        else
        {
            requestBuilder.Append("Content-Length: 0\r\n");
        }

        requestBuilder.Append("\r\n");

        // Write request.
        var headerBytes = Encoding.ASCII.GetBytes(requestBuilder.ToString());
        await pipe.WriteAsync(headerBytes);
        if (bodyBytes != null)
        {
            await pipe.WriteAsync(bodyBytes);
        }
        await pipe.FlushAsync();
        Logger.Debug($"[BrokerClient] Wrote {headerBytes.Length + (bodyBytes?.Length ?? 0)} bytes to pipe, reading response...");

        // Read response.
        var readCts = new CancellationTokenSource(READ_TIMEOUT_MS);
        return await ReadHttpResponseAsync(pipe, readCts.Token);
    }

    /// <summary>
    /// Parse an HTTP/1.1 response from the pipe stream.
    /// </summary>
    private static async Task<HttpPipeResponse> ReadHttpResponseAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[65536];
        var totalRead = 0;

        // Read until we have at least the headers.
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            Logger.Debug($"[BrokerClient] Pipe read: {bytesRead} bytes (total so far: {totalRead + bytesRead})");
            if (bytesRead == 0)
            {
                Logger.Debug($"[BrokerClient] Pipe returned 0 bytes (closed). Total read: {totalRead}");
                if (totalRead > 0)
                {
                    Logger.Debug($"[BrokerClient] Raw data so far: {Encoding.UTF8.GetString(buffer, 0, Math.Min(totalRead, 500))}");
                }
                break;
            }
            totalRead += bytesRead;

            // Check if we have the end of headers.
            var currentText = Encoding.ASCII.GetString(buffer, 0, totalRead);
            var headerEnd = currentText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd >= 0)
            {
                var headerText = currentText[..headerEnd];
                var bodyStart = headerEnd + 4;

                // Parse status line.
                var lines = headerText.Split("\r\n");
                var statusLine = lines[0];
                var statusCode = int.Parse(statusLine.Split(' ')[1]);

                // Parse headers.
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    var colonIdx = lines[i].IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = lines[i][..colonIdx].Trim();
                        var value = lines[i][(colonIdx + 1)..].Trim();
                        headers[key] = value;
                    }
                }

                // Read body based on Content-Length.
                int contentLength = 0;
                if (headers.TryGetValue("Content-Length", out var clStr))
                {
                    contentLength = int.Parse(clStr);
                }

                var bodyBytesRead = totalRead - bodyStart;
                while (bodyBytesRead < contentLength)
                {
                    var remaining = contentLength - bodyBytesRead;
                    if (bodyStart + bodyBytesRead + remaining > buffer.Length)
                    {
                        // Grow buffer if needed.
                        var newBuffer = new byte[bodyStart + contentLength];
                        Buffer.BlockCopy(buffer, 0, newBuffer, 0, totalRead);
                        buffer = newBuffer;
                    }

                    var read = await stream.ReadAsync(
                        buffer.AsMemory(bodyStart + bodyBytesRead, remaining), ct);
                    if (read == 0) break;
                    bodyBytesRead += read;
                    totalRead += read;
                }

                var bodyText = Encoding.UTF8.GetString(buffer, bodyStart, contentLength);
                return new HttpPipeResponse(statusCode, headers, bodyText);
            }
        }

        throw new InvalidOperationException("Failed to read complete HTTP response from pipe");
    }

    private readonly record struct HttpPipeResponse(
        int StatusCode,
        Dictionary<string, string> Headers,
        string Body);
#endif
}
