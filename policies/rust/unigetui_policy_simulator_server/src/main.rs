use unigetui_policy_simulator_core::{
    parse_args, DEFAULT_PIPE_NAME, DEFAULT_SERVER_URL, PolicyPathResolver,
};
use unigetui_policy_simulator_server::{
    bind_target_from_url, build_app, build_named_pipe_metadata, load_state,
    load_state_with_metadata, serve_named_pipe,
};

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let parsed_args = parse_args(std::env::args().skip(1));
    let policies_root = PolicyPathResolver::find_policies_root()?;
    let policy_argument = parsed_args
        .get("policy")
        .ok_or("Missing required --policy <path> argument.")?;
    let policy_path = PolicyPathResolver::resolve_existing_path(policy_argument)?;
    let policy_schema_path = parsed_args
        .get("policy-schema")
        .map(|value| PolicyPathResolver::resolve_existing_path(value))
        .transpose()?
        .unwrap_or_else(|| policies_root.join("schemas").join("unigetui.package-policy.schema.1.0.json"));
    let request_schema_path = parsed_args
        .get("request-schema")
        .map(|value| PolicyPathResolver::resolve_existing_path(value))
        .transpose()?
        .unwrap_or_else(|| policies_root.join("schemas").join("unigetui.package-request.schema.1.0.json"));

    if parsed_args.contains_key("url") && parsed_args.contains_key("pipe-name") {
        return Err("Specify either --url or --pipe-name, not both.".into());
    }

    if let Some(pipe_name) = parsed_args.get("pipe-name") {
        let state = load_state_with_metadata(
            &policy_path,
            &policy_schema_path,
            &request_schema_path,
            build_named_pipe_metadata(pipe_name),
        )?;
        let app = build_app(state.clone());
        let listening_on = state
            .metadata
            .pipe_name
            .as_deref()
            .unwrap_or(DEFAULT_PIPE_NAME);

        println!("UniGetUI Rust policy server simulator listening on named pipe {listening_on}");
        println!("Policy: {}", state.policy_path.display());
        serve_named_pipe(app, listening_on).await?;
        return Ok(());
    }

    let url = parsed_args
        .get("url")
        .cloned()
        .unwrap_or_else(|| DEFAULT_SERVER_URL.to_string());

    let state = load_state(&policy_path, &policy_schema_path, &request_schema_path)?;
    let app = build_app(state.clone());

    let bind_target = bind_target_from_url(&url)?;
    let listener = tokio::net::TcpListener::bind(bind_target).await?;
    let listening_on = listener.local_addr()?;

    println!("UniGetUI Rust policy server simulator listening on http://{listening_on}");
    println!("Policy: {}", state.policy_path.display());
    axum::serve(listener, app).await?;
    Ok(())
}
