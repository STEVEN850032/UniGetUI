use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PolicyDocument {
    #[serde(rename = "policyVersion")]
    pub policy_version: String,
    #[serde(rename = "policyType")]
    pub policy_type: String,
    pub metadata: PolicyMetadata,
    pub enforcement: PolicyEnforcement,
    pub rules: Vec<PolicyRule>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PolicyMetadata {
    pub id: String,
    pub publisher: String,
    pub revision: i32,
    #[serde(rename = "publishedAt")]
    pub published_at: String,
    #[serde(rename = "validFrom")]
    pub valid_from: Option<String>,
    #[serde(rename = "validUntil")]
    pub valid_until: Option<String>,
    pub description: Option<String>,
    #[serde(rename = "supportUrl")]
    pub support_url: Option<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PolicyEnforcement {
    #[serde(rename = "defaultDecision")]
    pub default_decision: String,
    #[serde(rename = "failureDecision")]
    pub failure_decision: String,
    #[serde(rename = "rulePrecedence")]
    pub rule_precedence: String,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PolicyRule {
    pub id: String,
    pub enabled: Option<bool>,
    pub priority: i32,
    pub decision: String,
    pub reason: Option<String>,
    #[serde(rename = "match")]
    pub match_criteria: PolicyMatch,
    pub constraints: Option<PolicyConstraints>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PolicyMatch {
    pub operations: Option<Vec<String>>,
    pub managers: Option<Vec<String>>,
    pub sources: Option<Vec<String>>,
    #[serde(rename = "packageIdentifiers")]
    pub package_identifiers: Option<Vec<String>>,
    #[serde(rename = "packageNames")]
    pub package_names: Option<Vec<String>>,
    pub versions: Option<Vec<String>>,
    #[serde(rename = "versionRange")]
    pub version_range: Option<VersionRange>,
    pub scopes: Option<Vec<String>>,
    pub architectures: Option<Vec<String>>,
    pub elevation: Option<Vec<String>>,
    #[serde(rename = "runAsAdministrator")]
    pub run_as_administrator: Option<Vec<bool>>,
    pub interactive: Option<Vec<bool>>,
    #[serde(rename = "skipHashCheck")]
    pub skip_hash_check: Option<Vec<bool>>,
    #[serde(rename = "preRelease")]
    pub pre_release: Option<Vec<bool>>,
    #[serde(rename = "hasCustomParameters")]
    pub has_custom_parameters: Option<Vec<bool>>,
    #[serde(rename = "hasCustomInstallLocation")]
    pub has_custom_install_location: Option<Vec<bool>>,
    #[serde(rename = "hasPrePostCommands")]
    pub has_pre_post_commands: Option<Vec<bool>>,
    #[serde(rename = "hasKillBeforeOperation")]
    pub has_kill_before_operation: Option<Vec<bool>>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct VersionRange {
    #[serde(rename = "minVersion")]
    pub min_version: Option<String>,
    #[serde(rename = "maxVersion")]
    pub max_version: Option<String>,
    #[serde(rename = "includePrerelease", default)]
    pub include_prerelease: bool,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PolicyConstraints {
    #[serde(rename = "allowInteractive")]
    pub allow_interactive: Option<bool>,
    #[serde(rename = "allowRunAsAdministrator")]
    pub allow_run_as_administrator: Option<bool>,
    #[serde(rename = "allowSkipHashCheck")]
    pub allow_skip_hash_check: Option<bool>,
    #[serde(rename = "allowPreRelease")]
    pub allow_pre_release: Option<bool>,
    #[serde(rename = "allowCustomInstallLocation")]
    pub allow_custom_install_location: Option<bool>,
    #[serde(rename = "allowedInstallLocationPatterns")]
    pub allowed_install_location_patterns: Option<Vec<String>>,
    #[serde(rename = "allowCustomParameters")]
    pub allow_custom_parameters: Option<bool>,
    #[serde(rename = "allowedCustomParameters")]
    pub allowed_custom_parameters: Option<Vec<String>>,
    #[serde(rename = "allowedCustomParameterPatterns")]
    pub allowed_custom_parameter_patterns: Option<Vec<String>>,
    #[serde(rename = "deniedCustomParameters")]
    pub denied_custom_parameters: Option<Vec<String>>,
    #[serde(rename = "allowPrePostCommands")]
    pub allow_pre_post_commands: Option<bool>,
    #[serde(rename = "allowKillBeforeOperation")]
    pub allow_kill_before_operation: Option<bool>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct PackageRequest {
    #[serde(rename = "requestVersion")]
    pub request_version: String,
    #[serde(rename = "requestType")]
    pub request_type: String,
    #[serde(rename = "requestId")]
    pub request_id: String,
    #[serde(rename = "createdAt")]
    pub created_at: String,
    pub operation: String,
    pub manager: RequestManager,
    pub source: RequestSource,
    pub package: RequestPackage,
    pub options: RequestOptions,
    pub broker: BrokerContext,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct RequestManager {
    pub name: String,
    #[serde(rename = "displayName")]
    pub display_name: Option<String>,
    #[serde(rename = "executableFriendlyName")]
    pub executable_friendly_name: Option<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct RequestSource {
    pub name: String,
    pub url: Option<String>,
    #[serde(rename = "isVirtualManager")]
    pub is_virtual_manager: Option<bool>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct RequestPackage {
    pub id: String,
    pub name: String,
    pub version: Option<String>,
    #[serde(rename = "newVersion")]
    pub new_version: Option<String>,
    pub channel: Option<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct RequestOptions {
    pub scope: Option<String>,
    pub architecture: Option<String>,
    pub version: Option<String>,
    #[serde(default)]
    pub interactive: bool,
    #[serde(rename = "runAsAdministrator")]
    #[serde(default)]
    pub run_as_administrator: bool,
    #[serde(rename = "skipHashCheck")]
    #[serde(default)]
    pub skip_hash_check: bool,
    #[serde(rename = "preRelease")]
    #[serde(default)]
    pub pre_release: bool,
    #[serde(rename = "customInstallLocation")]
    pub custom_install_location: Option<String>,
    #[serde(rename = "customParameters")]
    pub custom_parameters: Option<Vec<String>>,
    #[serde(rename = "preOperationCommand")]
    pub pre_operation_command: Option<String>,
    #[serde(rename = "postOperationCommand")]
    pub post_operation_command: Option<String>,
    #[serde(rename = "killBeforeOperation")]
    pub kill_before_operation: Option<Vec<String>>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct BrokerContext {
    #[serde(rename = "requestedElevation")]
    pub requested_elevation: String,
    #[serde(rename = "effectiveUser")]
    pub effective_user: Option<String>,
    #[serde(rename = "clientVersion")]
    pub client_version: Option<String>,
    #[serde(rename = "clientProcessPath")]
    pub client_process_path: Option<String>,
}

#[derive(Clone, Debug)]
pub struct LoadedDocument<T> {
    pub path: String,
    pub format: String,
    pub canonical_json: String,
    pub value: T,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct MatchedRule {
    pub id: String,
    pub priority: i32,
    pub decision: String,
    pub reason: Option<String>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PolicyDecision {
    pub decision: String,
    pub rule_id: String,
    pub priority: Option<i32>,
    pub reason: String,
    pub matched_rules: Vec<MatchedRule>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BrokerEvaluationResponse {
    pub request_id: String,
    pub manager: Option<String>,
    pub source: Option<String>,
    pub package_id: Option<String>,
    pub operation: Option<String>,
    pub decision: String,
    pub rule_id: String,
    pub reason: String,
    pub would_execute: bool,
    pub command: Vec<String>,
    pub mode: Option<String>,
}
