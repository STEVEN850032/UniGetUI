use std::net::SocketAddr;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use axum::extract::{Request, State};
use axum::http::{header, HeaderMap, HeaderValue, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{Json, Router};
use chrono::Utc;
#[cfg(windows)]
use hyper::server::conn::http1;
#[cfg(windows)]
use hyper_util::rt::TokioIo;
#[cfg(windows)]
use hyper_util::service::TowerToHyperService;
use serde::Serialize;
use serde_json::json;
#[cfg(windows)]
use tokio::net::windows::named_pipe::{NamedPipeServer, ServerOptions};
use unigetui_policy_simulator_core::{
    BrokerEnvelope, BrokerSimulator, DocumentLoader, EnvelopeMetadata, PackageRequest,
    PolicyDocument, PolicyError, PolicyEvaluator, Result, HTTP_LOOPBACK_TRANSPORT,
    NAMED_PIPE_TRANSPORT, PROTOCOL_VERSION, REQUEST_MEDIA_TYPE, REQUEST_SCHEMA_URL,
    RESPONSE_MEDIA_TYPE, RESPONSE_SCHEMA_URL,
};
use uuid::Uuid;

pub const SERVER_NAME: &str = "UniGetUI Rust policy server simulator";
pub const ALLOW_NOTE: &str = "The sample server returns the command that an elevated broker would run; it does not execute package managers.";
pub const VALIDATION_NOTE: &str = "The sample server validates and filters requests but never executes package managers.";

#[derive(Clone)]
pub struct AppState {
    pub policy_path: PathBuf,
    pub request_schema_path: PathBuf,
    pub policy: PolicyDocument,
    pub broker: Arc<BrokerSimulator>,
    pub metadata: EnvelopeMetadata,
}

pub fn load_state(
    policy_path: &Path,
    policy_schema_path: &Path,
    request_schema_path: &Path,
) -> Result<Arc<AppState>> {
    load_state_with_metadata(
        policy_path,
        policy_schema_path,
        request_schema_path,
        build_http_metadata(),
    )
}

pub fn load_state_with_metadata(
    policy_path: &Path,
    policy_schema_path: &Path,
    request_schema_path: &Path,
    metadata: EnvelopeMetadata,
) -> Result<Arc<AppState>> {
    let loader = DocumentLoader;
    let loaded_policy = loader.load_file::<PolicyDocument>(policy_path, Some(policy_schema_path))?;
    PolicyEvaluator::validate_policy_shape(&loaded_policy.value)?;

    Ok(Arc::new(AppState {
        policy_path: policy_path.to_path_buf(),
        request_schema_path: request_schema_path.to_path_buf(),
        policy: loaded_policy.value.clone(),
        broker: Arc::new(BrokerSimulator::new(loaded_policy.value)),
        metadata,
    }))
}

pub fn build_app(state: Arc<AppState>) -> Router {
    Router::new()
        .route("/health", get(health))
        .route(unigetui_policy_simulator_core::HEALTH_ENDPOINT, get(health))
        .route(unigetui_policy_simulator_core::CAPABILITIES_ENDPOINT, get(capabilities))
        .route(unigetui_policy_simulator_core::LEGACY_EVALUATE_ENDPOINT, post(handle_package_operation))
        .route(unigetui_policy_simulator_core::EVALUATE_ENDPOINT, post(handle_package_operation))
        .with_state(state)
}

pub fn bind_target_from_url(url: &str) -> std::result::Result<SocketAddr, Box<dyn std::error::Error>> {
    let address_text = url
        .strip_prefix("http://")
        .or_else(|| url.strip_prefix("https://"))
        .unwrap_or(url);
    Ok(address_text.parse()?)
}

pub fn build_http_metadata() -> EnvelopeMetadata {
    EnvelopeMetadata {
        broker_name: SERVER_NAME.to_string(),
        protocol_version: PROTOCOL_VERSION.to_string(),
        transport: HTTP_LOOPBACK_TRANSPORT.to_string(),
        pipe_name: None,
        elevated_simulation: true,
    }
}

pub fn build_named_pipe_metadata(pipe_name: &str) -> EnvelopeMetadata {
    EnvelopeMetadata {
        broker_name: SERVER_NAME.to_string(),
        protocol_version: PROTOCOL_VERSION.to_string(),
        transport: NAMED_PIPE_TRANSPORT.to_string(),
        pipe_name: Some(normalize_pipe_name(pipe_name)),
        elevated_simulation: true,
    }
}

pub fn normalize_pipe_name(pipe_name: &str) -> String {
    if pipe_name.starts_with(r"\\.\pipe\") {
        pipe_name.to_string()
    } else {
        format!(r"\\.\pipe\{}", pipe_name)
    }
}

#[cfg(windows)]
pub async fn serve_named_pipe(app: Router, pipe_name: &str) -> Result<()> {
    let normalized_pipe_name = normalize_pipe_name(pipe_name);
    let mut first_pipe_instance = true;

    loop {
        let server = create_named_pipe_server(&normalized_pipe_name, first_pipe_instance)?;
        first_pipe_instance = false;

        server.connect().await.map_err(|error| {
            PolicyError::validation(format!(
                "Failed to accept named-pipe connection on '{}': {}",
                normalized_pipe_name,
                error
            ))
        })?;

        let service = TowerToHyperService::new(app.clone());
        tokio::spawn(async move {
            let io = TokioIo::new(server);
            if let Err(error) = http1::Builder::new().keep_alive(false).serve_connection(io, service).await {
                eprintln!("Named-pipe connection failed: {error}");
            }
        });
    }
}

#[cfg(not(windows))]
pub async fn serve_named_pipe(_app: Router, pipe_name: &str) -> Result<()> {
    Err(PolicyError::validation(format!(
        "Named-pipe transport is only available on Windows. Requested pipe '{}'.",
        pipe_name
    )))
}

#[cfg(windows)]
fn create_named_pipe_server(pipe_name: &str, first_pipe_instance: bool) -> Result<NamedPipeServer> {
    let mut options = ServerOptions::new();
    if first_pipe_instance {
        options.first_pipe_instance(true);
    }

    options.create(pipe_name).map_err(|error| {
        PolicyError::validation(format!(
            "Failed to create named-pipe server '{}': {}",
            pipe_name,
            error
        ))
    })
}

async fn health(State(state): State<Arc<AppState>>) -> Json<serde_json::Value> {
    Json(json!({
        "status": "ready",
        "protocolVersion": PROTOCOL_VERSION,
        "elevatedSimulation": true,
        "transport": state.metadata.transport,
        "pipeName": state.metadata.pipe_name,
        "policyPath": state.policy_path.display().to_string(),
        "endpoints": [
            unigetui_policy_simulator_core::HEALTH_ENDPOINT,
            unigetui_policy_simulator_core::CAPABILITIES_ENDPOINT,
            unigetui_policy_simulator_core::EVALUATE_ENDPOINT,
            unigetui_policy_simulator_core::LEGACY_EVALUATE_ENDPOINT
        ]
    }))
}

async fn capabilities() -> Json<serde_json::Value> {
    Json(json!({
        "protocolVersion": PROTOCOL_VERSION,
        "transports": [HTTP_LOOPBACK_TRANSPORT, NAMED_PIPE_TRANSPORT],
        "requestMediaTypes": [REQUEST_MEDIA_TYPE, "application/json"],
        "responseMediaTypes": [RESPONSE_MEDIA_TYPE],
        "requestSchema": REQUEST_SCHEMA_URL,
        "responseSchema": RESPONSE_SCHEMA_URL,
        "supportedManagers": ["Winget", "PowerShell"],
        "supportedOperations": ["install", "update", "uninstall"],
        "maxRequestBodyBytes": 262144
    }))
}

async fn handle_package_operation(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    request: Request,
) -> Response {
    let audit_id = create_audit_id();
    let content_type = headers
        .get(header::CONTENT_TYPE)
        .and_then(|value| value.to_str().ok())
        .map(str::to_string);

    let body = match axum::body::to_bytes(request.into_body(), usize::MAX).await {
        Ok(body) => body,
        Err(error) => {
            return envelope_response(
                StatusCode::UNPROCESSABLE_ENTITY,
                None,
                &audit_id,
                &state.policy,
                validation_failure_envelope(&state.policy, &state.metadata, &audit_id, &error.to_string()),
            )
        }
    };

    let text = String::from_utf8_lossy(&body).to_string();
    if text.trim().is_empty() {
        return envelope_response(
            StatusCode::BAD_REQUEST,
            None,
            &audit_id,
            &state.policy,
            validation_failure_envelope(&state.policy, &state.metadata, &audit_id, "Request body is required."),
        );
    }

    let format = DocumentLoader::infer_format_from_content_type(content_type.as_deref());
    let loader = DocumentLoader;
    let request = match loader.load_text::<PackageRequest>(
        &text,
        "HTTP request body",
        &format,
        Some(state.request_schema_path.as_path()),
    ) {
        Ok(request) => request.value,
        Err(error) => {
            return envelope_response(
                StatusCode::UNPROCESSABLE_ENTITY,
                None,
                &audit_id,
                &state.policy,
                validation_failure_envelope(&state.policy, &state.metadata, &audit_id, &error.to_string()),
            )
        }
    };

    if let Err(error) = validate_request_headers(&headers, &request) {
        return envelope_response(
            StatusCode::UNPROCESSABLE_ENTITY,
            None,
            &audit_id,
            &state.policy,
            validation_failure_envelope(&state.policy, &state.metadata, &audit_id, &error),
        );
    }

    let broker_response = state.broker.evaluate(&request);
    let status = if broker_response.decision == "allow" {
        StatusCode::OK
    } else {
        StatusCode::FORBIDDEN
    };

    envelope_response(
        status,
        Some(&broker_response.request_id),
        &audit_id,
        &state.policy,
        success_envelope(&state.policy, &state.metadata, &audit_id, &broker_response),
    )
}

fn validate_request_headers(headers: &HeaderMap, request: &PackageRequest) -> std::result::Result<(), String> {
    if let Some(header_value) = headers
        .get("UniGetUI-Request-Id")
        .and_then(|value| value.to_str().ok())
        .filter(|value| !value.is_empty())
    {
        if header_value != request.request_id {
            return Err("Header 'UniGetUI-Request-Id' must match request body field 'requestId'.".to_string());
        }
    }

    Ok(())
}

fn envelope_response<T: Serialize>(
    status: StatusCode,
    request_id: Option<&str>,
    audit_id: &str,
    policy: &PolicyDocument,
    envelope: T,
) -> Response {
    let mut response = (status, Json(envelope)).into_response();
    let headers = response.headers_mut();
    headers.insert(header::CONTENT_TYPE, HeaderValue::from_static(RESPONSE_MEDIA_TYPE));
    headers.insert("UniGetUI-Protocol-Version", HeaderValue::from_static(PROTOCOL_VERSION));
    headers.insert(
        "UniGetUI-Audit-Id",
        HeaderValue::from_str(audit_id).unwrap_or_else(|_| HeaderValue::from_static("audit-invalid")),
    );
    headers.insert(
        "UniGetUI-Policy-Id",
        HeaderValue::from_str(&policy.metadata.id).unwrap_or_else(|_| HeaderValue::from_static("invalid-policy-id")),
    );
    headers.insert(
        "UniGetUI-Policy-Revision",
        HeaderValue::from_str(&policy.metadata.revision.to_string()).unwrap_or_else(|_| HeaderValue::from_static("0")),
    );
    if let Some(request_id) = request_id.filter(|value| !value.is_empty()) {
        if let Ok(value) = HeaderValue::from_str(request_id) {
            headers.insert("UniGetUI-Request-Id", value);
        }
    }
    response
}

fn success_envelope(
    policy: &PolicyDocument,
    metadata: &EnvelopeMetadata,
    audit_id: &str,
    response: &unigetui_policy_simulator_core::BrokerEvaluationResponse,
) -> BrokerEnvelope {
    let timestamp = Utc::now().to_rfc3339();
    BrokerEnvelope::from_evaluation(policy, audit_id, response, metadata, ALLOW_NOTE, &timestamp)
}

fn validation_failure_envelope(
    policy: &PolicyDocument,
    metadata: &EnvelopeMetadata,
    audit_id: &str,
    reason: &str,
) -> BrokerEnvelope {
    let timestamp = Utc::now().to_rfc3339();
    BrokerEnvelope::validation_failure(policy, audit_id, reason, metadata, VALIDATION_NOTE, &timestamp)
}

fn create_audit_id() -> String {
    format!("audit-{}", Uuid::new_v4().simple())
}
