use std::env;
use std::path::{Path, PathBuf};

use crate::error::{PolicyError, Result};

pub struct PolicyPathResolver;

impl PolicyPathResolver {
    pub fn resolve_existing_path(path: impl AsRef<Path>) -> Result<PathBuf> {
        let path = path.as_ref();
        if path.exists() {
            return path.canonicalize().map_err(|source| PolicyError::Io {
                document_name: path.display().to_string(),
                source,
            });
        }

        let current_candidate = env::current_dir()
            .map_err(|source| PolicyError::Io {
                document_name: "current directory".to_string(),
                source,
            })?
            .join(path);
        if current_candidate.exists() {
            return current_candidate.canonicalize().map_err(|source| PolicyError::Io {
                document_name: current_candidate.display().to_string(),
                source,
            });
        }

        Err(PolicyError::validation(format!(
            "Path '{}' does not exist.",
            path.display()
        )))
    }

    pub fn find_policies_root() -> Result<PathBuf> {
        let mut candidates = Vec::new();
        candidates.push(env::current_dir().map_err(|source| PolicyError::Io {
            document_name: "current directory".to_string(),
            source,
        })?);
        candidates.push(PathBuf::from(env!("CARGO_MANIFEST_DIR")));

        for start in candidates {
            for ancestor in start.ancestors() {
                if is_policies_root(ancestor) {
                    return Ok(ancestor.to_path_buf());
                }
            }
        }

        Err(PolicyError::validation(
            "Could not locate the policies root containing schemas and samples.",
        ))
    }
}

fn is_policies_root(candidate: &Path) -> bool {
    candidate
        .join("schemas")
        .join("unigetui.package-policy.schema.1.0.json")
        .is_file()
        && candidate.join("samples").is_dir()
}
