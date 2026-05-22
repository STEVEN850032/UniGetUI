use std::collections::HashMap;

pub fn parse_args(arguments: impl Iterator<Item = String>) -> HashMap<String, String> {
    let args = arguments.collect::<Vec<_>>();
    let mut result = HashMap::new();
    let mut index = 0usize;
    while index < args.len() {
        let current = &args[index];
        if !current.starts_with("--") {
            index += 1;
            continue;
        }

        let key = current.trim_start_matches("--").to_string();
        if index + 1 >= args.len() || args[index + 1].starts_with("--") {
            result.insert(key, "true".to_string());
            index += 1;
            continue;
        }

        result.insert(key, args[index + 1].clone());
        index += 2;
    }

    result
}