// ABOUTME: UDMF universal text parser; first slice of UDB UniversalParser porting.
// ABOUTME: Parses global fields and element blocks with bool/int/float/string values.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

#[derive(Clone, Debug, PartialEq)]
pub enum UdmfValue {
    Bool(bool),
    Int(i64),
    Float(f64),
    String(String),
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct UdmfBlock {
    pub name: String,
    pub fields: Vec<(String, UdmfValue)>,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct UdmfDocument {
    pub fields: Vec<(String, UdmfValue)>,
    pub blocks: Vec<UdmfBlock>,
}

impl UdmfDocument {
    pub fn namespace(&self) -> Option<&str> {
        self.fields
            .iter()
            .find(|(k, _)| k == "namespace")
            .and_then(|(_, v)| match v {
                UdmfValue::String(s) => Some(s.as_str()),
                _ => None,
            })
    }
}

#[derive(Debug, PartialEq, Eq)]
pub struct UdmfError(pub String);

struct Lexer<'a> {
    s: &'a [u8],
    pos: usize,
}

impl<'a> Lexer<'a> {
    fn skip_ws(&mut self) {
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

    fn ident(&mut self) -> Option<String> {
        self.skip_ws();
        let start = self.pos;
        while self.pos < self.s.len() {
            let c = self.s[self.pos] as char;
            if c.is_ascii_alphanumeric() || c == '_' {
                self.pos += 1;
            } else {
                break;
            }
        }
        if self.pos == start {
            None
        } else {
            Some(String::from_utf8_lossy(&self.s[start..self.pos]).to_lowercase())
        }
    }

    fn expect(&mut self, c: u8) -> Result<(), UdmfError> {
        self.skip_ws();
        if self.pos < self.s.len() && self.s[self.pos] == c {
            self.pos += 1;
            Ok(())
        } else {
            Err(UdmfError(format!(
                "expected '{}' at byte {}",
                c as char, self.pos
            )))
        }
    }

    fn peek(&mut self) -> Option<u8> {
        self.skip_ws();
        self.s.get(self.pos).copied()
    }

    fn value(&mut self) -> Result<UdmfValue, UdmfError> {
        self.skip_ws();
        if self.peek() == Some(b'"') {
            self.pos += 1;
            let mut out = String::new();
            while self.pos < self.s.len() && self.s[self.pos] != b'"' {
                if self.s[self.pos] == b'\\' && self.pos + 1 < self.s.len() {
                    self.pos += 1;
                }
                out.push(self.s[self.pos] as char);
                self.pos += 1;
            }
            self.expect(b'"')?;
            return Ok(UdmfValue::String(out));
        }
        let start = self.pos;
        while self.pos < self.s.len()
            && self.s[self.pos] != b';'
            && !(self.s[self.pos] as char).is_whitespace()
        {
            self.pos += 1;
        }
        let raw = String::from_utf8_lossy(&self.s[start..self.pos]).to_string();
        let lower = raw.to_lowercase();
        if lower == "true" {
            return Ok(UdmfValue::Bool(true));
        }
        if lower == "false" {
            return Ok(UdmfValue::Bool(false));
        }
        if let Some(hex) = lower.strip_prefix("0x") {
            if let Ok(i) = i64::from_str_radix(hex, 16) {
                return Ok(UdmfValue::Int(i));
            }
        }
        if let Ok(i) = raw.parse::<i64>() {
            return Ok(UdmfValue::Int(i));
        }
        if let Ok(f) = raw.parse::<f64>() {
            return Ok(UdmfValue::Float(f));
        }
        Err(UdmfError(format!(
            "invalid value '{}' at byte {}",
            raw, start
        )))
    }
}

/// Parse a UDMF TEXTMAP document into global fields and element blocks.
pub fn parse(text: &str) -> Result<UdmfDocument, UdmfError> {
    let mut lex = Lexer {
        s: text.as_bytes(),
        pos: 0,
    };
    let mut doc = UdmfDocument::default();

    loop {
        lex.skip_ws();
        if lex.pos >= lex.s.len() {
            return Ok(doc);
        }
        let name = lex
            .ident()
            .ok_or_else(|| UdmfError(format!("expected identifier at byte {}", lex.pos)))?;
        match lex.peek() {
            Some(b'=') => {
                lex.expect(b'=')?;
                let v = lex.value()?;
                lex.expect(b';')?;
                doc.fields.push((name, v));
            }
            Some(b'{') => {
                lex.expect(b'{')?;
                let mut block = UdmfBlock {
                    name,
                    fields: Vec::new(),
                };
                while lex.peek() != Some(b'}') {
                    let key = lex
                        .ident()
                        .ok_or_else(|| UdmfError(format!("expected field at byte {}", lex.pos)))?;
                    lex.expect(b'=')?;
                    let v = lex.value()?;
                    lex.expect(b';')?;
                    block.fields.push((key, v));
                }
                lex.expect(b'}')?;
                doc.blocks.push(block);
            }
            _ => return Err(UdmfError(format!("expected '=' or '{{' after '{}'", name))),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_namespace_and_vertex_block() {
        let doc =
            parse("namespace = \"zdoom\";\n\nvertex // first\n{\n x = 0.000;\n y = -64.000;\n}\n")
                .unwrap();
        assert_eq!(Some("zdoom"), doc.namespace());
        assert_eq!(1, doc.blocks.len());
        assert_eq!("vertex", doc.blocks[0].name);
        assert_eq!(("x".into(), UdmfValue::Float(0.0)), doc.blocks[0].fields[0]);
        assert_eq!(
            ("y".into(), UdmfValue::Float(-64.0)),
            doc.blocks[0].fields[1]
        );
    }

    #[test]
    fn parses_bool_int_string_and_escapes() {
        let doc =
            parse("thing { skill1 = true; type = 1; comment = \"say \\\"hi\\\"\"; angle = 0; }")
                .unwrap();
        let f = &doc.blocks[0].fields;
        assert_eq!(("skill1".into(), UdmfValue::Bool(true)), f[0]);
        assert_eq!(("type".into(), UdmfValue::Int(1)), f[1]);
        assert_eq!(
            ("comment".into(), UdmfValue::String("say \"hi\"".into())),
            f[2]
        );
    }

    #[test]
    fn skips_block_comments_and_crlf() {
        let doc = parse("/* header */\r\nnamespace = \"doom\";\r\nlinedef { v1 = 0; v2 = 1; blocking = true; }\r\n").unwrap();
        assert_eq!(Some("doom"), doc.namespace());
        assert_eq!("linedef", doc.blocks[0].name);
    }

    #[test]
    fn rejects_malformed_input() {
        assert!(parse("vertex { x = ; }").is_err());
        assert!(parse("vertex [").is_err());
    }

    #[test]
    fn keys_fold_to_lowercase_like_udb() {
        let doc = parse("Vertex { X = 1; }").unwrap();
        assert_eq!("vertex", doc.blocks[0].name);
        assert_eq!("x", doc.blocks[0].fields[0].0);
    }
}
