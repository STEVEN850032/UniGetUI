use thiserror::Error;

pub type Result<T> = std::result::Result<T, PolicyError>;

#[derive(Debug, Error)]
pub enum PolicyError {
    #[error("{0}")]
    Validation(String),

    #[error("Document '{document_name}' failed to read: {source}")]
    Io {
        document_name: String,
        #[source]
        source: std::io::Error,
    },

    #[error("Document '{document_name}' failed to parse as JSON: {source}")]
    Json {
        document_name: String,
        #[source]
        source: serde_json::Error,
    },

    #[error("Document '{document_name}' failed to parse as YAML: {message}")]
    Yaml {
        document_name: String,
        message: String,
    },

    #[error("Document '{document_name}' failed schema validation: {message}")]
    Schema {
        document_name: String,
        message: String,
    },
}

impl PolicyError {
    pub fn validation(message: impl Into<String>) -> Self {
        Self::Validation(message.into())
    }
}
