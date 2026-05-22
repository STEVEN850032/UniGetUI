use std::fs;

use reqwest::header::{ACCEPT, CONTENT_TYPE, HeaderMap, HeaderValue};
use unigetui_policy_simulator_core::{
    parse_args, DocumentLoader, PackageRequest, PolicyPathResolver, DEFAULT_SERVER_URL,
    EVALUATE_ENDPOINT, PROTOCOL_VERSION, REQUEST_MEDIA_TYPE, RESPONSE_MEDIA_TYPE,
};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let parsed_args = parse_args(std::env::args().skip(1));
    let request_path = parsed_args
        .get("request")
        .ok_or("Missing required --request <path> argument.")?;
    let server = parsed_args
        .get("server")
        .cloned()
        .unwrap_or_else(|| DEFAULT_SERVER_URL.to_string());
    let endpoint = parsed_args
        .get("endpoint")
        .cloned()
        .unwrap_or_else(|| EVALUATE_ENDPOINT.to_string());
    let as_json = parsed_args.contains_key("json");

    let full_request_path = PolicyPathResolver::resolve_existing_path(request_path)?;
    let request_text = fs::read_to_string(&full_request_path)?;
    let format = DocumentLoader::infer_format_from_path(&full_request_path)?;
    let content_type = if format == "yaml" {
        "application/x-yaml"
    } else {
        REQUEST_MEDIA_TYPE
    };
    let loader = DocumentLoader;
    let loaded_request = loader.load_text::<PackageRequest>(
        &request_text,
        &full_request_path.display().to_string(),
        &format,
        None,
    )?;

    let mut default_headers = HeaderMap::new();
    default_headers.insert(ACCEPT, HeaderValue::from_static(RESPONSE_MEDIA_TYPE));
    default_headers.insert(
        "UniGetUI-Protocol-Version",
        HeaderValue::from_static(PROTOCOL_VERSION),
    );
    if let Ok(value) = HeaderValue::from_str(&loaded_request.value.request_id) {
        default_headers.insert("UniGetUI-Request-Id", value);
    }

    let client = reqwest::Client::builder().default_headers(default_headers).build()?;
    let response = client
        .post(join_server_and_endpoint(&server, &endpoint))
        .header(CONTENT_TYPE, content_type)
        .body(request_text)
        .send()
        .await?;

    let status = response.status();
    let response_text = response.text().await?;

    if as_json {
        println!("{response_text}");
    } else {
        println!("HTTP {} {}", status.as_u16(), status.canonical_reason().unwrap_or(""));
        println!("{response_text}");
    }

    if status.is_success()
        || status == reqwest::StatusCode::FORBIDDEN
        || status == reqwest::StatusCode::BAD_REQUEST
        || status == reqwest::StatusCode::UNPROCESSABLE_ENTITY
    {
        Ok(())
    } else {
        Err(format!("Unexpected HTTP status {}.", status).into())
    }
}

fn join_server_and_endpoint(server: &str, endpoint: &str) -> String {
    let normalized_server = server.trim_end_matches('/');
    let normalized_endpoint = if endpoint.starts_with('/') {
        endpoint.to_string()
    } else {
        format!("/{endpoint}")
    };
    format!("{normalized_server}{normalized_endpoint}")
}
