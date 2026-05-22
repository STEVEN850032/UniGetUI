use unigetui_policy_simulator_core::{parse_args, PolicyPathResolver, Result};

#[tokio::main]
async fn main() {
    if let Err(error) = run().await {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

async fn run() -> Result<()> {
    let parsed_args = parse_args(std::env::args().skip(1));
    let policy_root = if let Some(argument) = parsed_args.get("policy-root") {
        PolicyPathResolver::resolve_existing_path(argument)?
    } else {
        PolicyPathResolver::find_policies_root()?
    };
    unigetui_policy_simulator_http_tests::run_with_policy_root(&policy_root).await?;
    Ok(())
}

