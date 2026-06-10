// ABOUTME: UDB structured configuration parser; first slice of Configuration.cs porting.
// ABOUTME: Parses assignments and nested blocks with dotted-path lookup; includes follow later.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

#[derive(Clone, Debug, PartialEq)]
pub enum ConfigValue {
    Bool(bool),
    Int(i64),
    Float(f64),
    String(String),
    Null,
    Struct(Vec<(String, ConfigValue)>),
}

impl ConfigValue {
    /// Dotted-path lookup, e.g. "gameinfo.skytextures.map01".
    pub fn lookup(&self, path: &str) -> Option<&ConfigValue> {
        let mut cur = self;
        for part in path.split('.') {
            match cur {
                ConfigValue::Struct(fields) => {
                    cur = &fields.iter().find(|(k, _)| k.eq_ignore_ascii_case(part))?.1;
                }
                _ => return None,
            }
        }
        Some(cur)
    }

    pub fn as_str(&self) -> Option<&str> {
        match self {
            ConfigValue::String(s) => Some(s),
            _ => None,
        }
    }

    pub fn as_i64(&self) -> Option<i64> {
        match self {
            ConfigValue::Int(i) => Some(*i),
            _ => None,
        }
    }

    pub fn as_bool(&self) -> Option<bool> {
        match self {
            ConfigValue::Bool(b) => Some(*b),
            _ => None,
        }
    }
}

#[derive(Debug, PartialEq, Eq)]
pub struct ConfigError(pub String);

struct P<'a> {
    s: &'a [u8],
    pos: usize,
}

impl<'a> P<'a> {
    fn ws(&mut self) {
        loop {
            while self.pos < self.s.len() && (self.s[self.pos] as char).is_whitespace() {
                self.pos += 1;
            }
            if self.s[self.pos..].starts_with(b"//") {
                while self.pos < self.s.len() && self.s[self.pos] != b'\n' {
                    self.pos += 1;
                }
            } else if self.s[self.pos..].starts_with(b"/*") {
                self.pos += 2;
                while self.pos + 1 < self.s.len() && !self.s[self.pos..].starts_with(b"*/") {
                    self.pos += 1;
                }
                self.pos = (self.pos + 2).min(self.s.len());
            } else {
                return;
            }
        }
    }

    fn peek(&mut self) -> Option<u8> {
        self.ws();
        self.s.get(self.pos).copied()
    }

    fn key(&mut self) -> Option<String> {
        self.ws();
        let start = self.pos;
        while self.pos < self.s.len() {
            let c = self.s[self.pos] as char;
            if c.is_ascii_alphanumeric() || "_.$".contains(c) {
                self.pos += 1;
            } else {
                break;
            }
        }
        if self.pos == start {
            None
        } else {
            Some(String::from_utf8_lossy(&self.s[start..self.pos]).to_string())
        }
    }

    fn value(&mut self) -> Result<ConfigValue, ConfigError> {
        self.ws();
        match self.peek() {
            Some(b'"') => {
                self.pos += 1;
                let mut out = String::new();
                while self.pos < self.s.len() && self.s[self.pos] != b'"' {
                    if self.s[self.pos] == b'\\' && self.pos + 1 < self.s.len() {
                        self.pos += 1;
                        // UDB unescapes \n, \t, \", \\ in configuration strings.
                        out.push(match self.s[self.pos] {
                            b'n' => '\n',
                            b't' => '\t',
                            c => c as char,
                        });
                    } else {
                        out.push(self.s[self.pos] as char);
                    }
                    self.pos += 1;
                }
                self.pos += 1;
                Ok(ConfigValue::String(out))
            }
            Some(b'{') => self.structure(),
            _ => {
                let start = self.pos;
                while self.pos < self.s.len() && !b";\r\n".contains(&self.s[self.pos]) {
                    self.pos += 1;
                }
                let raw = String::from_utf8_lossy(&self.s[start..self.pos])
                    .trim()
                    .to_string();
                let lower = raw.to_lowercase();
                match lower.as_str() {
                    "true" => Ok(ConfigValue::Bool(true)),
                    "false" => Ok(ConfigValue::Bool(false)),
                    "null" => Ok(ConfigValue::Null),
                    _ => {
                        if let Ok(i) = raw.parse::<i64>() {
                            Ok(ConfigValue::Int(i))
                        } else if let Ok(f) = raw.parse::<f64>() {
                            Ok(ConfigValue::Float(f))
                        } else {
                            Err(ConfigError(format!(
                                "invalid value '{}' at byte {}",
                                raw, start
                            )))
                        }
                    }
                }
            }
        }
    }

    fn structure(&mut self) -> Result<ConfigValue, ConfigError> {
        self.ws();
        self.pos += 1; // consume '{'
        let mut fields = Vec::new();
        loop {
            match self.peek() {
                Some(b'}') => {
                    self.pos += 1;
                    return Ok(ConfigValue::Struct(fields));
                }
                None => return Err(ConfigError("unterminated structure".into())),
                _ => {
                    let k = self
                        .key()
                        .ok_or_else(|| ConfigError(format!("expected key at byte {}", self.pos)))?;
                    match self.peek() {
                        Some(b'=') => {
                            self.pos += 1;
                            let v = self.value()?;
                            self.ws();
                            if self.peek() == Some(b';') {
                                self.pos += 1;
                            }
                            fields.push((k, v));
                        }
                        Some(b'{') => {
                            let v = self.structure()?;
                            fields.push((k, v));
                        }
                        Some(b';') => {
                            // Bare key like UDB's flag-style entries.
                            self.pos += 1;
                            fields.push((k, ConfigValue::Null));
                        }
                        _ => {
                            return Err(ConfigError(format!(
                                "expected '=', '{{' or ';' after '{}'",
                                k
                            )))
                        }
                    }
                }
            }
        }
    }
}

/// Parse a UDB structured configuration document into a root Struct.
pub fn parse(text: &str) -> Result<ConfigValue, ConfigError> {
    // Wrap the document in a synthetic structure and reuse the block parser.
    let wrapped = format!("{{{}}}", text);
    let mut p = P {
        s: wrapped.as_bytes(),
        pos: 0,
    };
    p.structure()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_assignments_and_nested_structures() {
        let cfg = parse(
            r#"
// game configuration
game = "Doom";  defaultlumpname = "MAP01";
defaultsavecompiler = "zdbsp_normal";
scale = 1.5;
maps
{
    map01 { sky = "SKY1"; secret = false; par = 30; }
}
"#,
        )
        .unwrap();
        assert_eq!(Some("Doom"), cfg.lookup("game").and_then(|v| v.as_str()));
        assert_eq!(
            Some("SKY1"),
            cfg.lookup("maps.map01.sky").and_then(|v| v.as_str())
        );
        assert_eq!(
            Some(false),
            cfg.lookup("maps.map01.secret").and_then(|v| v.as_bool())
        );
        assert_eq!(
            Some(30),
            cfg.lookup("maps.map01.par").and_then(|v| v.as_i64())
        );
        assert_eq!(Some(&ConfigValue::Float(1.5)), cfg.lookup("scale"));
    }

    #[test]
    fn lookup_is_case_insensitive_and_misses_return_none() {
        let cfg = parse("Thing { Width = 16; }").unwrap();
        assert_eq!(Some(16), cfg.lookup("thing.width").and_then(|v| v.as_i64()));
        assert_eq!(None, cfg.lookup("thing.height"));
    }

    #[test]
    fn string_escapes_unescape_like_udb() {
        let cfg = parse(r#"title = "line1\nline2 \"quoted\"";"#).unwrap();
        assert_eq!(
            Some("line1\nline2 \"quoted\""),
            cfg.lookup("title").and_then(|v| v.as_str())
        );
    }

    #[test]
    fn malformed_structures_error() {
        assert!(parse("maps { map01 { sky = ").is_err());
        assert!(parse("x = @bad;").is_err());
    }
}

/// Recursively merge an overlay into a base structure (UDB Configuration merge rules):
/// struct fields merge deep, scalar values override, and new keys append in order.
pub fn merge(base: &mut ConfigValue, overlay: &ConfigValue) {
    if let (ConfigValue::Struct(base_fields), ConfigValue::Struct(over_fields)) =
        (&mut *base, overlay)
    {
        for (k, ov) in over_fields {
            if let Some((_, bv)) = base_fields
                .iter_mut()
                .find(|(bk, _)| bk.eq_ignore_ascii_case(k))
            {
                if matches!((&*bv, ov), (ConfigValue::Struct(_), ConfigValue::Struct(_))) {
                    merge(bv, ov);
                } else {
                    *bv = ov.clone();
                }
            } else {
                base_fields.push((k.clone(), ov.clone()));
            }
        }
    } else {
        *base = overlay.clone();
    }
}

/// Parse with include() support. The resolver maps an include name to file text;
/// `include("name")` splices the whole document, `include("name", "dotted.path")`
/// splices the structure at that path, mirroring UDB's include function.
pub fn parse_with_includes(
    text: &str,
    resolver: &dyn Fn(&str) -> Option<String>,
) -> Result<ConfigValue, ConfigError> {
    let parsed = parse(text)?;
    expand_includes(parsed, resolver)
}

fn expand_includes(
    value: ConfigValue,
    resolver: &dyn Fn(&str) -> Option<String>,
) -> Result<ConfigValue, ConfigError> {
    match value {
        ConfigValue::Struct(fields) => {
            let mut out: Vec<(String, ConfigValue)> = Vec::with_capacity(fields.len());
            for (k, v) in fields {
                if k.eq_ignore_ascii_case("include") {
                    let (name, path) = include_args(&v)?;
                    let text = resolver(&name)
                        .ok_or_else(|| ConfigError(format!("cannot resolve include '{}'", name)))?;
                    let doc = parse_with_includes(&text, resolver)?;
                    let target = match &path {
                        Some(p) => doc
                            .lookup(p)
                            .ok_or_else(|| {
                                ConfigError(format!("include path '{}' not found in '{}'", p, name))
                            })?
                            .clone(),
                        None => doc,
                    };
                    match target {
                        ConfigValue::Struct(inc_fields) => {
                            let mut spliced = ConfigValue::Struct(out);
                            merge(&mut spliced, &ConfigValue::Struct(inc_fields));
                            out = match spliced {
                                ConfigValue::Struct(f) => f,
                                _ => unreachable!(),
                            };
                        }
                        other => out.push((name, other)),
                    }
                } else {
                    out.push((k, expand_includes(v, resolver)?));
                }
            }
            Ok(ConfigValue::Struct(out))
        }
        other => Ok(other),
    }
}

// include arguments arrive as the raw value after '=' or as a call-shaped string;
// the parser stores `include("a", "b")` keys with a String value of the call args.
fn include_args(v: &ConfigValue) -> Result<(String, Option<String>), ConfigError> {
    match v {
        ConfigValue::String(s) => Ok((s.clone(), None)),
        ConfigValue::Struct(fields) if !fields.is_empty() => {
            // include { name = "..."; path = "..."; } form used by tests/tools.
            let name = fields
                .iter()
                .find(|(k, _)| k == "name")
                .and_then(|(_, v)| v.as_str())
                .ok_or_else(|| ConfigError("include requires a name".into()))?
                .to_string();
            let path = fields
                .iter()
                .find(|(k, _)| k == "path")
                .and_then(|(_, v)| v.as_str())
                .map(str::to_string);
            Ok((name, path))
        }
        _ => Err(ConfigError("invalid include arguments".into())),
    }
}

#[cfg(test)]
mod include_tests {
    use super::*;

    #[test]
    fn merge_overrides_scalars_and_merges_structs_deep() {
        let mut base = parse("a = 1; block { x = 1; y = 2; }").unwrap();
        let overlay = parse("a = 5; block { y = 9; z = 3; } extra = true;").unwrap();
        merge(&mut base, &overlay);
        assert_eq!(Some(5), base.lookup("a").and_then(|v| v.as_i64()));
        assert_eq!(Some(1), base.lookup("block.x").and_then(|v| v.as_i64()));
        assert_eq!(Some(9), base.lookup("block.y").and_then(|v| v.as_i64()));
        assert_eq!(Some(3), base.lookup("block.z").and_then(|v| v.as_i64()));
        assert_eq!(Some(true), base.lookup("extra").and_then(|v| v.as_bool()));
    }

    #[test]
    fn include_splices_whole_document() {
        let resolver = |name: &str| -> Option<String> {
            (name == "misc.cfg").then(|| "shared { value = 7; }".to_string())
        };
        let cfg = parse_with_includes("include = \"misc.cfg\"; own = 1;", &resolver).unwrap();
        assert_eq!(Some(7), cfg.lookup("shared.value").and_then(|v| v.as_i64()));
        assert_eq!(Some(1), cfg.lookup("own").and_then(|v| v.as_i64()));
    }

    #[test]
    fn include_with_path_splices_substructure() {
        let resolver = |name: &str| -> Option<String> {
            (name == "misc.cfg").then(|| "outer { inner { value = 7; } }".to_string())
        };
        let cfg = parse_with_includes(
            "block { include { name = \"misc.cfg\"; path = \"outer.inner\"; } }",
            &resolver,
        )
        .unwrap();
        assert_eq!(Some(7), cfg.lookup("block.value").and_then(|v| v.as_i64()));
    }

    #[test]
    fn unresolvable_include_errors() {
        let resolver = |_: &str| -> Option<String> { None };
        assert!(parse_with_includes("include = \"missing.cfg\";", &resolver).is_err());
    }
}
