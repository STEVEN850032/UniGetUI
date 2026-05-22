use std::fs;
use std::path::Path;
#[cfg(windows)]
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use reqwest::header::{ACCEPT, CONTENT_TYPE, HeaderMap, HeaderValue};
#[cfg(windows)]
use serde_json::Value;
#[cfg(windows)]
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::task::JoinHandle;
#[cfg(windows)]
use tokio::net::windows::named_pipe::{ClientOptions, NamedPipeClient};
use unigetui_policy_simulator_core::{
    BrokerEnvelope, DocumentLoader, PackageRequest, PolicyError, Result,
    CAPABILITIES_ENDPOINT, EVALUATE_ENDPOINT, HEALTH_ENDPOINT, NAMED_PIPE_TRANSPORT,
    PROTOCOL_VERSION, REQUEST_MEDIA_TYPE, RESPONSE_MEDIA_TYPE,
};
use unigetui_policy_simulator_server::{
    build_app, build_named_pipe_metadata, load_state, load_state_with_metadata,
    serve_named_pipe,
};

pub async fn run_with_policy_root(policy_root: &Path) -> Result<()> {
    let policy_path = policy_root.join("samples").join("corporate-allowlist.policy.json");
    let policy_schema_path = policy_root.join("schemas").join("unigetui.package-policy.schema.1.0.json");
    let request_schema_path = policy_root.join("schemas").join("unigetui.package-request.schema.1.0.json");
    let response_schema_path = policy_root.join("schemas").join("unigetui.package-broker-response.schema.1.0.json");
    let allow_request_path = policy_root.join("samples").join("requests").join("winget-vscode-install.request.json");
    let deny_request_path = policy_root.join("samples").join("requests").join("winget-unknown-install.request.json");

    let state = load_state(&policy_path, &policy_schema_path, &request_schema_path)?;
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.map_err(|error| {
        PolicyError::validation(format!("Failed to bind test listener: {}", error))
    })?;
    let server_address = listener.local_addr().map_err(|error| {
        PolicyError::validation(format!("Failed to inspect test listener address: {}", error))
    })?;
    let app = build_app(state);
    let server_task = tokio::spawn(async move {
        let _ = axum::serve(listener, app).await;
    });

    let server_url = format!("http://{}", server_address);
    let client = reqwest::Client::new();
    let loader = DocumentLoader;

    let health = client.get(format!("{server_url}{HEALTH_ENDPOINT}")).send().await.map_err(http_error)?;
    if health.status() != reqwest::StatusCode::OK {
        shutdown_server(server_task);
        return Err(PolicyError::validation(format!(
            "Health endpoint returned {}.",
            health.status()
        )));
    }

    let capabilities = client
        .get(format!("{server_url}{CAPABILITIES_ENDPOINT}"))
        .send()
        .await
        .map_err(http_error)?;
    if capabilities.status() != reqwest::StatusCode::OK {
        shutdown_server(server_task);
        return Err(PolicyError::validation(format!(
            "Capabilities endpoint returned {}.",
            capabilities.status()
        )));
    }

    let allow_request = load_request(&loader, &allow_request_path)?;
    let allow_response = post_request(&client, &server_url, &allow_request).await?;
    validate_live_response(
        &loader,
        &response_schema_path,
        "live allow response",
        allow_response,
        reqwest::StatusCode::OK,
        true,
    )
    .await?;

    let deny_request = load_request(&loader, &deny_request_path)?;
    let deny_response = post_request(&client, &server_url, &deny_request).await?;
    validate_live_response(
        &loader,
        &response_schema_path,
        "live deny response",
        deny_response,
        reqwest::StatusCode::FORBIDDEN,
        false,
    )
    .await?;

    let mismatched_header_response = post_request_with_header(
        &client,
        &server_url,
        &allow_request,
        Some("req-mismatch-header"),
    )
    .await?;
    validate_live_response(
        &loader,
        &response_schema_path,
        "live validation response",
        mismatched_header_response,
        reqwest::StatusCode::UNPROCESSABLE_ENTITY,
        false,
    )
    .await?;

    let empty_body_response = post_raw_request(
        &client,
        &server_url,
        REQUEST_MEDIA_TYPE,
        "",
        Some("req-empty-body"),
    )
    .await?;
    validate_live_response(
        &loader,
        &response_schema_path,
        "live empty body response",
        empty_body_response,
        reqwest::StatusCode::BAD_REQUEST,
        false,
    )
    .await?;

    let malformed_body_response = post_raw_request(
        &client,
        &server_url,
        REQUEST_MEDIA_TYPE,
        "{",
        Some("req-malformed-body"),
    )
    .await?;
    validate_live_response(
        &loader,
        &response_schema_path,
        "live malformed body response",
        malformed_body_response,
        reqwest::StatusCode::UNPROCESSABLE_ENTITY,
        false,
    )
    .await?;

    let malformed_yaml_response = post_raw_request(
        &client,
        &server_url,
        "application/x-yaml",
        "requestVersion: 1.0.0\nrequestType: [packageOperation\n",
        Some("req-malformed-yaml"),
    )
    .await?;
    validate_live_response(
        &loader,
        &response_schema_path,
        "live malformed yaml response",
        malformed_yaml_response,
        reqwest::StatusCode::UNPROCESSABLE_ENTITY,
        false,
    )
    .await?;

    shutdown_server(server_task);
    #[cfg(windows)]
    run_named_pipe_checks(
        &policy_path,
        &policy_schema_path,
        &request_schema_path,
        &response_schema_path,
        &allow_request,
    )
    .await?;

    println!("Live HTTP checks passed: 8");
    #[cfg(windows)]
    println!("Named-pipe checks passed: 5");
    Ok(())
}

#[cfg(windows)]
async fn run_named_pipe_checks(
    policy_path: &Path,
    policy_schema_path: &Path,
    request_schema_path: &Path,
    response_schema_path: &Path,
    allow_request: &LoadedRequest,
) -> Result<()> {
    let pipe_name = unique_pipe_name();
    let state = load_state_with_metadata(
        policy_path,
        policy_schema_path,
        request_schema_path,
        build_named_pipe_metadata(&pipe_name),
    )?;
    let normalized_pipe_name = state
        .metadata
        .pipe_name
        .clone()
        .ok_or_else(|| PolicyError::validation("Named-pipe metadata is missing the pipe name.".to_string()))?;
    let app = build_app(state);
    let pipe_name_for_server = normalized_pipe_name.clone();
    let server_task = tokio::spawn(async move {
        let _ = serve_named_pipe(app, &pipe_name_for_server).await;
    });

    let health_request = b"GET /v1/health HTTP/1.1\r\nHost: unigetui-broker\r\nAccept: application/json\r\nConnection: close\r\n\r\n";
    let health_response = send_named_pipe_request(&normalized_pipe_name, health_request).await?;
    validate_named_pipe_health_response(&health_response, &normalized_pipe_name)?;

    let allow_request_bytes = build_named_pipe_evaluate_request(allow_request);
    let allow_response = send_named_pipe_request(&normalized_pipe_name, &allow_request_bytes).await?;
    validate_named_pipe_broker_response(
        &DocumentLoader,
        response_schema_path,
        "named-pipe allow response",
        &normalized_pipe_name,
        &allow_response,
        "HTTP/1.1 200 OK",
        true,
    )?;

    let mismatched_header_request = build_named_pipe_post_request(
        &allow_request.content_type,
        allow_request.body.as_bytes(),
        Some("req-mismatch-header"),
    );
    let mismatched_header_response = send_named_pipe_request(&normalized_pipe_name, &mismatched_header_request).await?;
    validate_named_pipe_broker_response(
        &DocumentLoader,
        response_schema_path,
        "named-pipe header mismatch response",
        &normalized_pipe_name,
        &mismatched_header_response,
        "HTTP/1.1 422 Unprocessable Entity",
        false,
    )?;

    let malformed_json_request = build_named_pipe_post_request(
        REQUEST_MEDIA_TYPE,
        b"{",
        Some("req-malformed-body"),
    );
    let malformed_json_response = send_named_pipe_request(&normalized_pipe_name, &malformed_json_request).await?;
    validate_named_pipe_broker_response(
        &DocumentLoader,
        response_schema_path,
        "named-pipe malformed body response",
        &normalized_pipe_name,
        &malformed_json_response,
        "HTTP/1.1 422 Unprocessable Entity",
        false,
    )?;

    let empty_body_request = build_named_pipe_post_request(
        REQUEST_MEDIA_TYPE,
        b"",
        Some("req-empty-body"),
    );
    let empty_body_response = send_named_pipe_request(&normalized_pipe_name, &empty_body_request).await?;
    validate_named_pipe_broker_response(
        &DocumentLoader,
        response_schema_path,
        "named-pipe empty body response",
        &normalized_pipe_name,
        &empty_body_response,
        "HTTP/1.1 400 Bad Request",
        false,
    )?;

    shutdown_server(server_task);
    Ok(())
}

fn load_request(loader: &DocumentLoader, request_path: &Path) -> Result<LoadedRequest> {
    let body = fs::read_to_string(request_path).map_err(|error| {
        PolicyError::validation(format!(
            "Failed to read request '{}': {}",
            request_path.display(),
            error
        ))
    })?;
    let format = DocumentLoader::infer_format_from_path(request_path)?;
    let request = loader.load_text::<PackageRequest>(&body, &request_path.display().to_string(), &format, None)?;
    Ok(LoadedRequest {
        request: request.value,
        body,
        content_type: if format == "yaml" { "application/x-yaml".to_string() } else { REQUEST_MEDIA_TYPE.to_string() },
    })
}

async fn post_request(client: &reqwest::Client, server_url: &str, request: &LoadedRequest) -> Result<reqwest::Response> {
    post_request_with_header(client, server_url, request, Some(&request.request.request_id)).await
}

async fn post_request_with_header(
    client: &reqwest::Client,
    server_url: &str,
    request: &LoadedRequest,
    request_id_header: Option<&str>,
) -> Result<reqwest::Response> {
    post_raw_request(
        client,
        server_url,
        &request.content_type,
        &request.body,
        request_id_header,
    )
    .await
}

async fn post_raw_request(
    client: &reqwest::Client,
    server_url: &str,
    content_type: &str,
    body: &str,
    request_id_header: Option<&str>,
) -> Result<reqwest::Response> {
    let mut headers = HeaderMap::new();
    headers.insert(ACCEPT, HeaderValue::from_static(RESPONSE_MEDIA_TYPE));
    headers.insert("UniGetUI-Protocol-Version", HeaderValue::from_static(PROTOCOL_VERSION));
    if let Some(request_id_header) = request_id_header {
        headers.insert(
            "UniGetUI-Request-Id",
            HeaderValue::from_str(request_id_header).map_err(|error| {
                PolicyError::validation(format!("Invalid request id header '{}': {}", request_id_header, error))
            })?,
        );
    }

    client
        .post(format!("{server_url}{EVALUATE_ENDPOINT}"))
        .headers(headers)
        .header(CONTENT_TYPE, content_type)
        .body(body.to_string())
        .send()
        .await
        .map_err(http_error)
}

async fn validate_live_response(
    loader: &DocumentLoader,
    response_schema_path: &Path,
    name: &str,
    response: reqwest::Response,
    expected_status: reqwest::StatusCode,
    expected_would_execute: bool,
) -> Result<()> {
    let status = response.status();
    let headers = response.headers().clone();
    let body = response.text().await.map_err(http_error)?;

    if status != expected_status {
        return Err(PolicyError::validation(format!(
            "{} returned {}, expected {}.",
            name,
            status,
            expected_status
        )));
    }
    validate_header(&headers, "content-type", RESPONSE_MEDIA_TYPE, name)?;
    validate_header(&headers, "UniGetUI-Protocol-Version", PROTOCOL_VERSION, name)?;
    ensure_header_present(&headers, "UniGetUI-Audit-Id", name)?;
    ensure_header_present(&headers, "UniGetUI-Policy-Id", name)?;
    ensure_header_present(&headers, "UniGetUI-Policy-Revision", name)?;

    let loaded = loader.load_text::<BrokerEnvelope>(&body, name, "json", Some(response_schema_path))?;
    if loaded.value.would_execute != expected_would_execute {
        return Err(PolicyError::validation(format!(
            "{} had wouldExecute={}, expected {}.",
            name,
            loaded.value.would_execute,
            expected_would_execute
        )));
    }
    if expected_status == reqwest::StatusCode::UNPROCESSABLE_ENTITY || expected_status == reqwest::StatusCode::BAD_REQUEST {
        if loaded.value.rule_id != "<validation-failure>" {
            return Err(PolicyError::validation(format!(
                "{} had ruleId='{}', expected '<validation-failure>'.",
                name,
                loaded.value.rule_id
            )));
        }
    }

    Ok(())
}

fn validate_header(headers: &reqwest::header::HeaderMap, name: &str, expected_value: &str, context: &str) -> Result<()> {
    let actual = headers
        .get(name)
        .and_then(|value| value.to_str().ok())
        .ok_or_else(|| PolicyError::validation(format!("{} missing header '{}'.", context, name)))?;
    if actual != expected_value {
        return Err(PolicyError::validation(format!(
            "{} header '{}' was '{}', expected '{}'.",
            context,
            name,
            actual,
            expected_value
        )));
    }
    Ok(())
}

fn ensure_header_present(headers: &reqwest::header::HeaderMap, name: &str, context: &str) -> Result<()> {
    headers
        .get(name)
        .and_then(|value| value.to_str().ok())
        .filter(|value| !value.is_empty())
        .ok_or_else(|| PolicyError::validation(format!("{} missing header '{}'.", context, name)))?;
    Ok(())
}

fn shutdown_server(server_task: JoinHandle<()>) {
    server_task.abort();
}

fn http_error(error: reqwest::Error) -> PolicyError {
    PolicyError::validation(format!("HTTP request failed: {}", error))
}

struct LoadedRequest {
    request: PackageRequest,
    body: String,
    content_type: String,
}

#[cfg(windows)]
async fn send_named_pipe_request(pipe_name: &str, request: &[u8]) -> Result<String> {
    let mut client = connect_named_pipe(pipe_name).await?;
    client.write_all(request).await.map_err(|error| {
        PolicyError::validation(format!("Failed to write request to named pipe '{}': {}", pipe_name, error))
    })?;
    client.flush().await.map_err(|error| {
        PolicyError::validation(format!("Failed to flush request to named pipe '{}': {}", pipe_name, error))
    })?;

    let mut response = Vec::new();
    client.read_to_end(&mut response).await.map_err(|error| {
        PolicyError::validation(format!("Failed to read response from named pipe '{}': {}", pipe_name, error))
    })?;

    Ok(String::from_utf8_lossy(&response).to_string())
}

#[cfg(windows)]
async fn connect_named_pipe(pipe_name: &str) -> Result<NamedPipeClient> {
    for attempt in 0..40 {
        match ClientOptions::new().open(pipe_name) {
            Ok(client) => return Ok(client),
            Err(error) if attempt < 39 => {
                tokio::time::sleep(Duration::from_millis(25)).await;
                let _ = error;
            }
            Err(error) => {
                return Err(PolicyError::validation(format!(
                    "Failed to connect to named pipe '{}': {}",
                    pipe_name,
                    error
                )))
            }
        }
    }

    Err(PolicyError::validation(format!(
        "Failed to connect to named pipe '{}' after repeated retries.",
        pipe_name
    )))
}

#[cfg(windows)]
fn validate_named_pipe_health_response(response: &str, pipe_name: &str) -> Result<()> {
    let parsed = parse_http_response(response, "named-pipe health response")?;
    if parsed.status_line != "HTTP/1.1 200 OK" {
        return Err(PolicyError::validation(format!(
            "Named-pipe health response returned '{}'.",
            parsed.status_line
        )));
    }

    let body: Value = serde_json::from_str(parsed.body).map_err(|error| {
        PolicyError::validation(format!("Named-pipe health response body was not valid JSON: {}", error))
    })?;
    if body.get("transport").and_then(Value::as_str) != Some(NAMED_PIPE_TRANSPORT) {
        return Err(PolicyError::validation(
            "Named-pipe health response did not advertise the named-pipe transport.".to_string(),
        ));
    }
    if body.get("pipeName").and_then(Value::as_str) != Some(pipe_name) {
        return Err(PolicyError::validation(format!(
            "Named-pipe health response reported pipeName='{}', expected '{}'.",
            body.get("pipeName").and_then(Value::as_str).unwrap_or("<missing>"),
            pipe_name
        )));
    }

    Ok(())
}

#[cfg(windows)]
fn build_named_pipe_evaluate_request(request: &LoadedRequest) -> Vec<u8> {
    build_named_pipe_post_request(
        &request.content_type,
        request.body.as_bytes(),
        Some(&request.request.request_id),
    )
}

#[cfg(windows)]
fn build_named_pipe_post_request(
    content_type: &str,
    body_bytes: &[u8],
    request_id_header: Option<&str>,
) -> Vec<u8> {
    let request_id_header = request_id_header
        .map(|value| format!("UniGetUI-Request-Id: {value}\r\n"))
        .unwrap_or_default();
    let headers = format!(
        "POST {EVALUATE_ENDPOINT} HTTP/1.1\r\nHost: unigetui-broker\r\nAccept: {RESPONSE_MEDIA_TYPE}\r\nContent-Type: {content_type}\r\nUniGetUI-Protocol-Version: {PROTOCOL_VERSION}\r\n{request_id_header}Connection: close\r\nContent-Length: {}\r\n\r\n",
        body_bytes.len()
    );

    let mut bytes = headers.into_bytes();
    bytes.extend_from_slice(body_bytes);
    bytes
}

#[cfg(windows)]
fn validate_named_pipe_broker_response(
    loader: &DocumentLoader,
    response_schema_path: &Path,
    context: &str,
    pipe_name: &str,
    response: &str,
    expected_status_line: &str,
    expected_would_execute: bool,
) -> Result<()> {
    let parsed = parse_http_response(response, context)?;
    if parsed.status_line != expected_status_line {
        return Err(PolicyError::validation(format!(
            "{} returned '{}', expected '{}'.",
            context,
            parsed.status_line
            , expected_status_line
        )));
    }
    if !parsed.headers_text.to_ascii_lowercase().contains(&format!("content-type: {}", RESPONSE_MEDIA_TYPE.to_ascii_lowercase())) {
        return Err(PolicyError::validation(
            format!("{} was missing the broker response media type header.", context),
        ));
    }
    if !parsed.headers_text.to_ascii_lowercase().contains(&format!("unigetui-protocol-version: {}", PROTOCOL_VERSION.to_ascii_lowercase())) {
        return Err(PolicyError::validation(
            format!("{} was missing the protocol version header.", context),
        ));
    }

    let loaded = loader.load_text::<BrokerEnvelope>(
        parsed.body,
        context,
        "json",
        Some(response_schema_path),
    )?;
    if loaded.value.would_execute != expected_would_execute {
        return Err(PolicyError::validation(
            format!(
                "{} had wouldExecute={}, expected {}.",
                context,
                loaded.value.would_execute,
                expected_would_execute
            ),
        ));
    }
    if loaded.value.broker.transport != NAMED_PIPE_TRANSPORT {
        return Err(PolicyError::validation(format!(
            "{} reported transport='{}', expected '{}'.",
            context,
            loaded.value.broker.transport,
            NAMED_PIPE_TRANSPORT
        )));
    }
    if loaded.value.broker.pipe_name.as_deref() != Some(pipe_name) {
        return Err(PolicyError::validation(format!(
            "{} reported pipeName='{}', expected '{}'.",
            context,
            loaded.value.broker.pipe_name.as_deref().unwrap_or("<missing>"),
            pipe_name
        )));
    }
    if !expected_would_execute && loaded.value.rule_id != "<validation-failure>" {
        return Err(PolicyError::validation(format!(
            "{} had ruleId='{}', expected '<validation-failure>'.",
            context,
            loaded.value.rule_id
        )));
    }

    Ok(())
}

#[cfg(windows)]
fn parse_http_response<'a>(response: &'a str, context: &str) -> Result<ParsedHttpResponse<'a>> {
    let (head, body) = response
        .split_once("\r\n\r\n")
        .or_else(|| response.split_once("\n\n"))
        .ok_or_else(|| PolicyError::validation(format!("{} did not contain a complete HTTP response.", context)))?;
    let mut lines = head.lines();
    let status_line = lines
        .next()
        .ok_or_else(|| PolicyError::validation(format!("{} was missing the HTTP status line.", context)))?;

    Ok(ParsedHttpResponse {
        status_line,
        headers_text: lines.collect::<Vec<_>>().join("\n"),
        body,
    })
}

#[cfg(windows)]
fn unique_pipe_name() -> String {
    let unique_suffix = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_else(|_| Duration::from_secs(0))
        .as_nanos();
    format!(r"\\.\pipe\UniGetUI.PackageBroker.Test.{}.{}", std::process::id(), unique_suffix)
}

#[cfg(windows)]
struct ParsedHttpResponse<'a> {
    status_line: &'a str,
    headers_text: String,
    body: &'a str,
}