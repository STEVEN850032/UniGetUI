use std::fs;
use std::path::Path;

use serde::de::DeserializeOwned;
use serde_json::Value as JsonValue;
use serde_yaml::Value as YamlValue;

use crate::error::{PolicyError, Result};
use crate::policy_models::LoadedDocument;

#[derive(Default)]
pub struct DocumentLoader;

impl DocumentLoader {
    pub fn load_file<T>(&self, path: &Path, schema_path: Option<&Path>) -> Result<LoadedDocument<T>>
    where
        T: DeserializeOwned,
    {
        let full_path = path.canonicalize().map_err(|source| PolicyError::Io {
            document_name: path.display().to_string(),
            source,
        })?;
        let text = fs::read_to_string(&full_path).map_err(|source| PolicyError::Io {
            document_name: full_path.display().to_string(),
            source,
        })?;
        self.load_text(
            &text,
            &full_path.display().to_string(),
            &Self::infer_format_from_path(&full_path)?,
            schema_path,
        )
    }

    pub fn load_text<T>(
        &self,
        text: &str,
        document_name: &str,
        format: &str,
        schema_path: Option<&Path>,
    ) -> Result<LoadedDocument<T>>
    where
        T: DeserializeOwned,
    {
        let canonical_json = self.convert_to_canonical_json(text, document_name, format)?;
        if let Some(schema_path) = schema_path {
            self.validate_json_schema(&canonical_json, schema_path, document_name)?;
        }

        let value = serde_json::from_str::<T>(&canonical_json).map_err(|source| PolicyError::Json {
            document_name: document_name.to_string(),
            source,
        })?;

        Ok(LoadedDocument {
            path: document_name.to_string(),
            format: format.to_string(),
            canonical_json,
            value,
        })
    }

    pub fn infer_format_from_path(path: &Path) -> Result<String> {
        match path.extension().and_then(|value| value.to_str()).map(|value| value.to_ascii_lowercase()) {
            Some(extension) if extension == "json" => Ok("json".to_string()),
            Some(extension) if extension == "yaml" || extension == "yml" => Ok("yaml".to_string()),
            Some(extension) => Err(PolicyError::validation(format!(
                "Unsupported document extension '.{}'. Use .json, .yaml, or .yml.",
                extension
            ))),
            None => Err(PolicyError::validation(
                "Unsupported document without an extension. Use .json, .yaml, or .yml.",
            )),
        }
    }

    pub fn infer_format_from_content_type(content_type: Option<&str>) -> String {
        let lowered = content_type.unwrap_or_default().to_ascii_lowercase();
        if lowered.contains("yaml") || lowered.contains("yml") {
            "yaml".to_string()
        } else {
            "json".to_string()
        }
    }

    fn convert_to_canonical_json(&self, text: &str, document_name: &str, format: &str) -> Result<String> {
        if format.eq_ignore_ascii_case("json") {
            let value = serde_json::from_str::<JsonValue>(text).map_err(|source| PolicyError::Json {
                document_name: document_name.to_string(),
                source,
            })?;
            return serde_json::to_string(&value).map_err(|source| PolicyError::Json {
                document_name: document_name.to_string(),
                source,
            });
        }

        if !format.eq_ignore_ascii_case("yaml") {
            return Err(PolicyError::validation(format!("Unsupported document format '{}'.", format)));
        }

        let yaml_value = serde_yaml::from_str::<YamlValue>(text).map_err(|source| PolicyError::Yaml {
            document_name: document_name.to_string(),
            message: source.to_string(),
        })?;
        let normalized = normalize_yaml_value(&yaml_value)?;
        serde_json::to_string(&normalized).map_err(|source| PolicyError::Json {
            document_name: document_name.to_string(),
            source,
        })
    }

    fn validate_json_schema(&self, canonical_json: &str, schema_path: &Path, document_name: &str) -> Result<()> {
        let schema_text = fs::read_to_string(schema_path).map_err(|source| PolicyError::Io {
            document_name: schema_path.display().to_string(),
            source,
        })?;
        let schema = serde_json::from_str::<JsonValue>(&schema_text).map_err(|source| PolicyError::Json {
            document_name: schema_path.display().to_string(),
            source,
        })?;
        let instance = serde_json::from_str::<JsonValue>(canonical_json).map_err(|source| PolicyError::Json {
            document_name: document_name.to_string(),
            source,
        })?;

        let validator = jsonschema::validator_for(&schema).map_err(|error| PolicyError::Schema {
            document_name: document_name.to_string(),
            message: error.to_string(),
        })?;

        let errors = validator
            .iter_errors(&instance)
            .map(|error| error.to_string())
            .collect::<Vec<_>>();

        if errors.is_empty() {
            Ok(())
        } else {
            Err(PolicyError::Schema {
                document_name: document_name.to_string(),
                message: errors.join("; "),
            })
        }
    }
}

fn normalize_yaml_value(value: &YamlValue) -> Result<JsonValue> {
    match value {
        YamlValue::Null => Ok(JsonValue::Null),
        YamlValue::Bool(value) => Ok(JsonValue::Bool(*value)),
        YamlValue::Number(value) => serde_json::to_value(value).map_err(|source| PolicyError::Json {
            document_name: "YAML number conversion".to_string(),
            source,
        }),
        YamlValue::String(value) => Ok(JsonValue::String(value.clone())),
        YamlValue::Sequence(values) => values
            .iter()
            .map(normalize_yaml_value)
            .collect::<Result<Vec<_>>>()
            .map(JsonValue::Array),
        YamlValue::Mapping(entries) => {
            let mut object = serde_json::Map::new();
            for (key, value) in entries {
                object.insert(yaml_key_to_string(key)?, normalize_yaml_value(value)?);
            }
            Ok(JsonValue::Object(object))
        }
        YamlValue::Tagged(tagged) => normalize_yaml_value(&tagged.value),
    }
}

fn yaml_key_to_string(value: &YamlValue) -> Result<String> {
    match value {
        YamlValue::Null => Ok(String::new()),
        YamlValue::Bool(value) => Ok(value.to_string()),
        YamlValue::Number(value) => Ok(value.to_string()),
        YamlValue::String(value) => Ok(value.clone()),
        YamlValue::Tagged(tagged) => yaml_key_to_string(&tagged.value),
        _ => Err(PolicyError::validation(
            "YAML object keys must normalize to a scalar string value.",
        )),
    }
}
