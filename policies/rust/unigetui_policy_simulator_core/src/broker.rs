use crate::command_line::CommandLineBuilder;
use crate::policy_evaluator::PolicyEvaluator;
use crate::policy_models::{BrokerEvaluationResponse, PackageRequest, PolicyDocument};

pub struct BrokerSimulator {
    policy: PolicyDocument,
    evaluator: PolicyEvaluator,
}

impl BrokerSimulator {
    pub fn new(policy: PolicyDocument) -> Self {
        Self {
            policy,
            evaluator: PolicyEvaluator,
        }
    }

    pub fn evaluate(&self, request: &PackageRequest) -> BrokerEvaluationResponse {
        match self.evaluator.evaluate(&self.policy, request) {
            Ok(decision) => {
                let command_result = if decision.decision == "allow" {
                    Some(CommandLineBuilder::build(request))
                } else {
                    None
                };

                if let Some(Err(error)) = command_result {
                    return BrokerEvaluationResponse {
                        request_id: request.request_id.clone(),
                        manager: Some(request.manager.name.clone()),
                        source: Some(request.source.name.clone()),
                        package_id: Some(request.package.id.clone()),
                        operation: Some(request.operation.clone()),
                        decision: "deny".to_string(),
                        rule_id: "<validation-failure>".to_string(),
                        reason: error.to_string(),
                        would_execute: false,
                        command: Vec::new(),
                        mode: Some("simulated-elevated".to_string()),
                    };
                }

                let command = command_result.transpose().ok().flatten().unwrap_or_default();

                BrokerEvaluationResponse {
                    request_id: request.request_id.clone(),
                    manager: Some(request.manager.name.clone()),
                    source: Some(request.source.name.clone()),
                    package_id: Some(request.package.id.clone()),
                    operation: Some(request.operation.clone()),
                    decision: decision.decision.clone(),
                    rule_id: decision.rule_id,
                    reason: decision.reason,
                    would_execute: decision.decision == "allow",
                    command,
                    mode: Some("simulated-elevated".to_string()),
                }
            }
            Err(error) => BrokerEvaluationResponse {
                request_id: request.request_id.clone(),
                manager: Some(request.manager.name.clone()),
                source: Some(request.source.name.clone()),
                package_id: Some(request.package.id.clone()),
                operation: Some(request.operation.clone()),
                decision: "deny".to_string(),
                rule_id: "<validation-failure>".to_string(),
                reason: error.to_string(),
                would_execute: false,
                command: Vec::new(),
                mode: Some("simulated-elevated".to_string()),
            },
        }
    }
}
