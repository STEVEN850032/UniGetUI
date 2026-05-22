use serde::{Deserialize, Serialize};

use crate::policy_models::{BrokerEvaluationResponse, PolicyDocument};

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
pub struct BrokerEnvelope {
    #[serde(rename = "$schema", skip_serializing_if = "Option::is_none")]
    pub schema: Option<String>,
    #[serde(rename = "responseVersion")]
    pub response_version: String,
    #[serde(rename = "responseType")]
    pub response_type: String,
    pub broker: BrokerInfo,
    #[serde(rename = "auditId")]
    pub audit_id: String,
    #[serde(rename = "requestId")]
    pub request_id: Option<String>,
    #[serde(rename = "receivedAt")]
    pub received_at: String,
    #[serde(rename = "completedAt")]
    pub completed_at: String,
    pub manager: Option<String>,
    pub source: Option<String>,
    #[serde(rename = "packageId")]
    pub package_id: Option<String>,
    pub operation: Option<String>,
    pub decision: String,
    #[serde(rename = "ruleId")]
    pub rule_id: String,
    pub reason: String,
    #[serde(rename = "wouldExecute")]
    pub would_execute: bool,
    pub policy: PolicyInfo,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub execution: Option<ExecutionInfo>,
}

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
pub struct BrokerInfo {
    pub name: String,
    #[serde(rename = "protocolVersion")]
    pub protocol_version: String,
    pub transport: String,
    #[serde(rename = "pipeName", skip_serializing_if = "Option::is_none")]
    pub pipe_name: Option<String>,
    #[serde(rename = "elevatedSimulation")]
    pub elevated_simulation: bool,
}

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
pub struct PolicyInfo {
    pub id: String,
    pub revision: i32,
    #[serde(rename = "policyVersion")]
    pub policy_version: String,
}

#[derive(Clone, Debug, Deserialize, Serialize, PartialEq, Eq)]
pub struct ExecutionInfo {
    pub mode: String,
    pub command: Vec<String>,
    pub note: String,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct EnvelopeMetadata {
    pub broker_name: String,
    pub protocol_version: String,
    pub transport: String,
    pub pipe_name: Option<String>,
    pub elevated_simulation: bool,
}

impl BrokerEnvelope {
    pub fn from_evaluation(
        policy: &PolicyDocument,
        audit_id: &str,
        response: &BrokerEvaluationResponse,
        metadata: &EnvelopeMetadata,
        note: &str,
        timestamp: &str,
    ) -> Self {
        Self {
            schema: None,
            response_version: "1.0.0".to_string(),
            response_type: "packageBrokerResponse".to_string(),
            broker: metadata.broker_info(),
            audit_id: audit_id.to_string(),
            request_id: Some(response.request_id.clone()),
            received_at: timestamp.to_string(),
            completed_at: timestamp.to_string(),
            manager: response.manager.clone(),
            source: response.source.clone(),
            package_id: response.package_id.clone(),
            operation: response.operation.clone(),
            decision: response.decision.clone(),
            rule_id: response.rule_id.clone(),
            reason: response.reason.clone(),
            would_execute: response.would_execute,
            policy: PolicyInfo::from_policy(policy),
            execution: response.mode.as_ref().map(|mode| ExecutionInfo {
                mode: mode.clone(),
                command: response.command.clone(),
                note: note.to_string(),
            }),
        }
    }

    pub fn validation_failure(
        policy: &PolicyDocument,
        audit_id: &str,
        reason: &str,
        metadata: &EnvelopeMetadata,
        note: &str,
        timestamp: &str,
    ) -> Self {
        Self {
            schema: None,
            response_version: "1.0.0".to_string(),
            response_type: "packageBrokerResponse".to_string(),
            broker: metadata.broker_info(),
            audit_id: audit_id.to_string(),
            request_id: None,
            received_at: timestamp.to_string(),
            completed_at: timestamp.to_string(),
            manager: None,
            source: None,
            package_id: None,
            operation: None,
            decision: "deny".to_string(),
            rule_id: "<validation-failure>".to_string(),
            reason: reason.to_string(),
            would_execute: false,
            policy: PolicyInfo::from_policy(policy),
            execution: Some(ExecutionInfo {
                mode: "simulated-elevated".to_string(),
                command: Vec::new(),
                note: note.to_string(),
            }),
        }
    }
}

impl EnvelopeMetadata {
    fn broker_info(&self) -> BrokerInfo {
        BrokerInfo {
            name: self.broker_name.clone(),
            protocol_version: self.protocol_version.clone(),
            transport: self.transport.clone(),
            pipe_name: self.pipe_name.clone(),
            elevated_simulation: self.elevated_simulation,
        }
    }
}

impl PolicyInfo {
    fn from_policy(policy: &PolicyDocument) -> Self {
        Self {
            id: policy.metadata.id.clone(),
            revision: policy.metadata.revision,
            policy_version: policy.policy_version.clone(),
        }
    }
}