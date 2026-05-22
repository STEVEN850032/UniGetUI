use std::env;

use unigetui_policy_simulator_core::{parse_args, PolicyPathResolver};

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), Box<dyn std::error::Error>> {
    let parsed_args = parse_args(env::args().skip(1));
    let policy_root = if let Some(argument) = parsed_args.get("policy-root") {
        PolicyPathResolver::resolve_existing_path(argument)?
    } else {
        PolicyPathResolver::find_policies_root()?
    };

    unigetui_policy_simulator_tests::run_with_policy_root(&policy_root)?;
    Ok(())
}
