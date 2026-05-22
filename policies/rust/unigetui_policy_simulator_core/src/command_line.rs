use crate::error::{PolicyError, Result};
use crate::policy_models::PackageRequest;

pub struct CommandLineBuilder;

impl CommandLineBuilder {
    pub fn build(request: &PackageRequest) -> Result<Vec<String>> {
        match request.manager.name.as_str() {
            "Winget" => Self::build_winget(request),
            "PowerShell" => Self::build_powershell(request),
            value => Err(PolicyError::validation(format!("Unsupported manager '{}'.", value))),
        }
    }

    fn build_winget(request: &PackageRequest) -> Result<Vec<String>> {
        let operation = match request.operation.as_str() {
            "install" => "install",
            "update" => "upgrade",
            "uninstall" => "uninstall",
            value => {
                return Err(PolicyError::validation(format!(
                    "Unsupported WinGet operation '{}'.",
                    value
                )))
            }
        };

        let mut command = vec![
            "winget.exe".to_string(),
            operation.to_string(),
            "--id".to_string(),
            request.package.id.clone(),
            "--exact".to_string(),
        ];
        add_pair(&mut command, "--source", request.source.name.as_str());
        add_optional_pair(&mut command, "--scope", request.options.scope.as_deref());
        add_optional_pair(&mut command, "--version", request.options.version.as_deref());
        command.push(if request.options.interactive { "--interactive" } else { "--silent" }.to_string());
        add_optional_pair(&mut command, "--architecture", request.options.architecture.as_deref());
        if request.options.skip_hash_check {
            command.push("--ignore-security-hash".to_string());
        }
        add_optional_pair(
            &mut command,
            "--location",
            request.options.custom_install_location.as_deref(),
        );
        if let Some(custom_parameters) = &request.options.custom_parameters {
            command.extend(custom_parameters.iter().cloned());
        }
        Ok(command)
    }

    fn build_powershell(request: &PackageRequest) -> Result<Vec<String>> {
        let verb = match request.operation.as_str() {
            "install" => "Install-Module",
            "update" => "Update-Module",
            "uninstall" => "Uninstall-Module",
            value => {
                return Err(PolicyError::validation(format!(
                    "Unsupported PowerShell operation '{}'.",
                    value
                )))
            }
        };

        let mut command = vec![
            "pwsh.exe".to_string(),
            "-NoProfile".to_string(),
            "-Command".to_string(),
            verb.to_string(),
            "-Name".to_string(),
            request.package.id.clone(),
        ];
        if request.operation == "install" && request.options.scope.as_deref() == Some("user") {
            command.extend(["-Scope".to_string(), "CurrentUser".to_string()]);
        }
        if request.operation == "install" && request.options.scope.as_deref() == Some("machine") {
            command.extend(["-Scope".to_string(), "AllUsers".to_string()]);
        }
        add_optional_pair(&mut command, "-RequiredVersion", request.options.version.as_deref());
        if request.options.pre_release {
            command.push("-AllowPrerelease".to_string());
        }
        if request.options.skip_hash_check {
            command.push("-SkipPublisherCheck".to_string());
        }
        if let Some(custom_parameters) = &request.options.custom_parameters {
            command.extend(custom_parameters.iter().cloned());
        }
        Ok(command)
    }
}

fn add_pair(command: &mut Vec<String>, name: &str, value: &str) {
    if value.trim().is_empty() {
        return;
    }
    command.push(name.to_string());
    command.push(value.to_string());
}

fn add_optional_pair(command: &mut Vec<String>, name: &str, value: Option<&str>) {
    if let Some(value) = value.filter(|value| !value.trim().is_empty()) {
        add_pair(command, name, value);
    }
}
