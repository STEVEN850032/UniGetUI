use regex::Regex;

use crate::error::{PolicyError, Result};
use crate::policy_models::{MatchedRule, PackageRequest, PolicyConstraints, PolicyDecision, PolicyDocument, PolicyRule, VersionRange};

#[derive(Default)]
pub struct PolicyEvaluator;

impl PolicyEvaluator {
    pub fn evaluate(&self, policy: &PolicyDocument, request: &PackageRequest) -> Result<PolicyDecision> {
        Self::validate_policy_shape(policy)?;
        Self::validate_request_shape(request)?;

        let mut matched_rules = Vec::new();
        for rule in &policy.rules {
            if rule.enabled == Some(false) {
                continue;
            }

            if self.rule_matches(rule, request)? {
                matched_rules.push(MatchedRule {
                    id: rule.id.clone(),
                    priority: rule.priority,
                    decision: rule.decision.clone(),
                    reason: rule.reason.clone(),
                });
            }
        }

        if matched_rules.is_empty() {
            let default_decision = normalized_default_decision(&policy.enforcement.default_decision);
            return Ok(PolicyDecision {
                decision: default_decision.to_string(),
                rule_id: "<default>".to_string(),
                priority: None,
                reason: format!(
                    "No enabled rule matched; using defaultDecision '{}'.",
                    default_decision
                ),
                matched_rules: Vec::new(),
            });
        }

        matched_rules.sort_by(|left, right| {
            left.priority
                .cmp(&right.priority)
                .then_with(|| deny_first_key(&left.decision).cmp(&deny_first_key(&right.decision)))
        });

        let winner = matched_rules[0].clone();
        Ok(PolicyDecision {
            decision: winner.decision.clone(),
            rule_id: winner.id.clone(),
            priority: Some(winner.priority),
            reason: winner.reason.unwrap_or_else(|| "Rule matched.".to_string()),
            matched_rules,
        })
    }

    pub fn validate_policy_shape(policy: &PolicyDocument) -> Result<()> {
        if policy.policy_type.trim().is_empty() {
            return Err(PolicyError::validation("Policy field 'policyType' is required."));
        }
        if policy.policy_type != "packageBrokerPolicy" {
            return Err(PolicyError::validation("Policy field 'policyType' must be 'packageBrokerPolicy'."));
        }
        if policy.policy_version.trim().is_empty() {
            return Err(PolicyError::validation("Policy field 'policyVersion' is required."));
        }
        if policy.metadata.id.trim().is_empty() {
            return Err(PolicyError::validation("Policy field 'metadata.id' is required."));
        }
        if policy.enforcement.failure_decision.trim().is_empty() {
            return Err(PolicyError::validation("Policy field 'enforcement.failureDecision' is required."));
        }
        if policy.enforcement.default_decision.trim().is_empty() {
            return Err(PolicyError::validation("Policy field 'enforcement.defaultDecision' is required."));
        }
        if policy.enforcement.rule_precedence.trim().is_empty() {
            return Err(PolicyError::validation("Policy field 'enforcement.rulePrecedence' is required."));
        }
        if policy.rules.is_empty() {
            return Err(PolicyError::validation("Policy field 'rules' must contain at least one rule."));
        }
        Ok(())
    }

    pub fn validate_request_shape(request: &PackageRequest) -> Result<()> {
        if request.request_type.trim().is_empty() {
            return Err(PolicyError::validation("Request field 'requestType' is required."));
        }
        if request.request_type != "packageOperation" {
            return Err(PolicyError::validation("Request field 'requestType' must be 'packageOperation'."));
        }
        if request.request_version.trim().is_empty() {
            return Err(PolicyError::validation("Request field 'requestVersion' is required."));
        }
        if request.request_id.trim().is_empty() {
            return Err(PolicyError::validation("Request field 'requestId' is required."));
        }
        if request.operation.trim().is_empty() {
            return Err(PolicyError::validation("Request operation is required."));
        }
        if request.manager.name.trim().is_empty() {
            return Err(PolicyError::validation("Request manager.name is required."));
        }
        if request.source.name.trim().is_empty() {
            return Err(PolicyError::validation("Request source.name is required."));
        }
        if request.package.id.trim().is_empty() {
            return Err(PolicyError::validation("Request package.id is required."));
        }
        if request.package.name.trim().is_empty() {
            return Err(PolicyError::validation("Request package.name is required."));
        }
        if request.broker.requested_elevation.trim().is_empty() {
            return Err(PolicyError::validation("Request broker.requestedElevation is required."));
        }
        Ok(())
    }

    fn rule_matches(&self, rule: &PolicyRule, request: &PackageRequest) -> Result<bool> {
        let flags = RequestFlags::from_request(request);
        let effective_version = effective_version(request);

        Ok(str_in_list(&request.operation, rule.match_criteria.operations.as_deref())
            && str_in_list(&request.manager.name, rule.match_criteria.managers.as_deref())
            && wildcard_any(Some(request.source.name.as_str()), rule.match_criteria.sources.as_deref())?
            && wildcard_any(Some(request.package.id.as_str()), rule.match_criteria.package_identifiers.as_deref())?
            && wildcard_any(Some(request.package.name.as_str()), rule.match_criteria.package_names.as_deref())?
            && str_in_list(&effective_version, rule.match_criteria.versions.as_deref())
            && version_range_matches(&effective_version, rule.match_criteria.version_range.as_ref())
            && optional_str_in_list(request.options.scope.as_deref(), rule.match_criteria.scopes.as_deref())
            && optional_str_in_list(request.options.architecture.as_deref(), rule.match_criteria.architectures.as_deref())
            && str_in_list(&request.broker.requested_elevation, rule.match_criteria.elevation.as_deref())
            && bool_in_list(request.options.run_as_administrator, rule.match_criteria.run_as_administrator.as_deref())
            && bool_in_list(request.options.interactive, rule.match_criteria.interactive.as_deref())
            && bool_in_list(request.options.skip_hash_check, rule.match_criteria.skip_hash_check.as_deref())
            && bool_in_list(request.options.pre_release, rule.match_criteria.pre_release.as_deref())
            && bool_in_list(flags.has_custom_parameters, rule.match_criteria.has_custom_parameters.as_deref())
            && bool_in_list(flags.has_custom_install_location, rule.match_criteria.has_custom_install_location.as_deref())
            && bool_in_list(flags.has_pre_post_commands, rule.match_criteria.has_pre_post_commands.as_deref())
            && bool_in_list(flags.has_kill_before_operation, rule.match_criteria.has_kill_before_operation.as_deref())
            && constraints_pass(rule.constraints.as_ref(), request, &flags)?)
    }
}

fn deny_first_key(decision: &str) -> i32 {
    if decision.eq_ignore_ascii_case("deny") { 0 } else { 1 }
}

fn normalized_default_decision(decision: &str) -> &'static str {
    if decision.eq_ignore_ascii_case("allow") {
        "allow"
    } else {
        "deny"
    }
}

fn str_in_list(value: &str, list: Option<&[String]>) -> bool {
    list.is_none_or(|items| items.iter().any(|item| item == value))
}

fn optional_str_in_list(value: Option<&str>, list: Option<&[String]>) -> bool {
    match list {
        None => true,
        Some(_) if value.is_none() => false,
        Some(items) => items.iter().any(|item| Some(item.as_str()) == value),
    }
}

fn bool_in_list(value: bool, list: Option<&[bool]>) -> bool {
    list.is_none_or(|items| items.contains(&value))
}

fn wildcard_any(value: Option<&str>, patterns: Option<&[String]>) -> Result<bool> {
    match patterns {
        None => Ok(true),
        Some(_) if value.is_none() => Ok(false),
        Some(patterns) => {
            let candidate = value.unwrap_or_default();
            for pattern in patterns {
                let regex = Regex::new(&format!("(?i)^{}$", regex::escape(pattern).replace("\\*", ".*")))
                    .map_err(|error| PolicyError::validation(format!("Invalid wildcard pattern '{}': {}", pattern, error)))?;
                if regex.is_match(candidate) {
                    return Ok(true);
                }
            }
            Ok(false)
        }
    }
}

fn effective_version(request: &PackageRequest) -> String {
    if let Some(version) = request.options.version.as_deref().filter(|value| !value.trim().is_empty()) {
        return version.to_string();
    }
    if let Some(version) = request.package.new_version.as_deref().filter(|value| !value.trim().is_empty()) {
        return version.to_string();
    }
    if let Some(version) = request.package.version.as_deref().filter(|value| !value.trim().is_empty()) {
        return version.to_string();
    }
    String::new()
}

fn version_range_matches(version: &str, range: Option<&VersionRange>) -> bool {
    let Some(range) = range else { return true; };
    if version.trim().is_empty() {
        return false;
    }
    if version.contains('-') && !range.include_prerelease {
        return false;
    }
    if let Some(minimum) = range.min_version.as_deref() {
        if compare_versions(version, minimum) < 0 {
            return false;
        }
    }
    if let Some(maximum) = range.max_version.as_deref() {
        if compare_versions(version, maximum) > 0 {
            return false;
        }
    }
    true
}

fn compare_versions(left: &str, right: &str) -> i32 {
    let normalized_left = left.split_once('-').map_or(left, |parts| parts.0);
    let normalized_right = right.split_once('-').map_or(right, |parts| parts.0);

    match (parse_numeric_version(normalized_left), parse_numeric_version(normalized_right)) {
        (Some(left_parts), Some(right_parts)) => compare_numeric_parts(&left_parts, &right_parts),
        _ => match normalized_left.to_ascii_lowercase().cmp(&normalized_right.to_ascii_lowercase()) {
            std::cmp::Ordering::Less => -1,
            std::cmp::Ordering::Equal => 0,
            std::cmp::Ordering::Greater => 1,
        },
    }
}

fn parse_numeric_version(version: &str) -> Option<Vec<u64>> {
    version
        .split('.')
        .map(|part| part.parse::<u64>().ok())
        .collect::<Option<Vec<_>>>()
}

fn compare_numeric_parts(left: &[u64], right: &[u64]) -> i32 {
    let max_len = left.len().max(right.len());
    for index in 0..max_len {
        let left_value = *left.get(index).unwrap_or(&0);
        let right_value = *right.get(index).unwrap_or(&0);
        match left_value.cmp(&right_value) {
            std::cmp::Ordering::Less => return -1,
            std::cmp::Ordering::Greater => return 1,
            std::cmp::Ordering::Equal => {}
        }
    }
    0
}

fn constraints_pass(constraints: Option<&PolicyConstraints>, request: &PackageRequest, flags: &RequestFlags) -> Result<bool> {
    let Some(constraints) = constraints else { return Ok(true); };
    if constraints.allow_interactive == Some(false) && request.options.interactive {
        return Ok(false);
    }
    if constraints.allow_run_as_administrator == Some(false) && request.options.run_as_administrator {
        return Ok(false);
    }
    if constraints.allow_skip_hash_check == Some(false) && request.options.skip_hash_check {
        return Ok(false);
    }
    if constraints.allow_pre_release == Some(false) && request.options.pre_release {
        return Ok(false);
    }
    if constraints.allow_custom_install_location == Some(false) && flags.has_custom_install_location {
        return Ok(false);
    }
    if constraints.allow_custom_parameters == Some(false) && flags.has_custom_parameters {
        return Ok(false);
    }
    if constraints.allow_pre_post_commands == Some(false) && flags.has_pre_post_commands {
        return Ok(false);
    }
    if constraints.allow_kill_before_operation == Some(false) && flags.has_kill_before_operation {
        return Ok(false);
    }

    if flags.has_custom_install_location
        && constraints.allowed_install_location_patterns.is_some()
        && !wildcard_any(
            Some(flags.custom_install_location.as_str()),
            constraints.allowed_install_location_patterns.as_deref(),
        )?
    {
        return Ok(false);
    }

    for parameter in &flags.custom_parameters {
        if wildcard_any(Some(parameter), constraints.denied_custom_parameters.as_deref())? {
            return Ok(false);
        }

        if constraints.allowed_custom_parameters.is_some() || constraints.allowed_custom_parameter_patterns.is_some() {
            let exact_allowed = str_in_list(parameter, constraints.allowed_custom_parameters.as_deref());
            let pattern_allowed = wildcard_any(Some(parameter), constraints.allowed_custom_parameter_patterns.as_deref())?;
            if !exact_allowed && !pattern_allowed {
                return Ok(false);
            }
        }
    }

    Ok(true)
}

#[derive(Debug)]
struct RequestFlags {
    has_custom_parameters: bool,
    has_custom_install_location: bool,
    has_pre_post_commands: bool,
    has_kill_before_operation: bool,
    custom_parameters: Vec<String>,
    custom_install_location: String,
}

impl RequestFlags {
    fn from_request(request: &PackageRequest) -> Self {
        let custom_parameters = request.options.custom_parameters.clone().unwrap_or_default();
        let custom_install_location = request.options.custom_install_location.clone().unwrap_or_default();
        Self {
            has_custom_parameters: !custom_parameters.is_empty(),
            has_custom_install_location: !custom_install_location.trim().is_empty(),
            has_pre_post_commands: request
                .options
                .pre_operation_command
                .as_deref()
                .is_some_and(|value| !value.trim().is_empty())
                || request
                    .options
                    .post_operation_command
                    .as_deref()
                    .is_some_and(|value| !value.trim().is_empty()),
            has_kill_before_operation: request
                .options
                .kill_before_operation
                .as_ref()
                .is_some_and(|items| !items.is_empty()),
            custom_parameters,
            custom_install_location,
        }
    }
}
