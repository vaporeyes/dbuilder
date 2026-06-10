// ABOUTME: MAPINFO/ZMAPINFO parsing; first slice of UDB MapinfoParser porting.
// ABOUTME: New-style DoomEdNums number-to-class mapping and map lump/title discovery.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

#[derive(Clone, Debug, Default, PartialEq)]
pub struct MapInfo {
    /// DoomEdNums entries: editor number to actor class name. Later entries
    /// override earlier ones for the same number like GZDoom.
    pub doomednums: Vec<(i32, String)>,
    /// Discovered maps: lump name (uppercased) and display title.
    pub maps: Vec<(String, String)>,
}

impl MapInfo {
    pub fn class_for(&self, num: i32) -> Option<&str> {
        self.doomednums
            .iter()
            .rev()
            .find(|(n, _)| *n == num)
            .map(|(_, c)| c.as_str())
    }
}

/// Parse new-style (Z)MAPINFO text. Old-style entries without braces are
/// skipped; DoomEdNums blocks and map headers are captured.
pub fn parse(text: &str) -> MapInfo {
    let mut info = MapInfo::default();
    let mut lines = text.lines().peekable();

    while let Some(line) = lines.next() {
        let code = strip_comment(line).trim().to_string();
        let lower = code.to_lowercase();

        if lower == "doomednums" || lower.starts_with("doomednums") {
            consume_block(&mut lines, |entry| {
                if let Some((num, class)) = entry.split_once('=') {
                    if let Ok(n) = num.trim().parse::<i32>() {
                        let class = class.trim().trim_matches('"').trim_end_matches(',');
                        if !class.is_empty() {
                            info.doomednums.push((n, class.to_string()));
                        }
                    }
                }
            });
        } else if lower.starts_with("map ") {
            // Forms: map MAP01 "Entryway" { ... } / map MAP01 lookup "..." { ... }
            let header = code[4..].trim();
            let rest = header.split('{').next().unwrap_or("").trim();
            let mut toks = rest.splitn(2, char::is_whitespace);
            let lump = toks.next().unwrap_or("").to_uppercase();
            let title = toks
                .next()
                .unwrap_or("")
                .trim()
                .trim_start_matches("lookup")
                .trim()
                .trim_matches('"')
                .to_string();
            if !lump.is_empty() {
                info.maps.push((lump, title));
            }
            if !code.contains('{') {
                // Block may start on a following line; consume it if present.
                if let Some(next) = lines.peek() {
                    if strip_comment(next).trim() == "{" {
                        consume_block(&mut lines, |_| {});
                    }
                }
            } else if !code.contains('}') {
                consume_open_block(&mut lines);
            }
        }
    }

    info
}

fn strip_comment(line: &str) -> &str {
    match line.find("//") {
        Some(i) => &line[..i],
        None => line,
    }
}

// Consume a `{ ... }` block whose opening brace is on the current or next line,
// invoking the callback per inner line.
fn consume_block<'a, I: Iterator<Item = &'a str>>(
    lines: &mut std::iter::Peekable<I>,
    mut f: impl FnMut(&str),
) {
    let mut depth = 0;
    for line in lines.by_ref() {
        let code = strip_comment(line).trim();
        for c in code.chars() {
            match c {
                '{' => depth += 1,
                '}' => depth -= 1,
                _ => {}
            }
        }
        if depth >= 1 && !code.contains('{') && !code.contains('}') {
            f(code);
        }
        if depth <= 0 && code.contains('}') {
            return;
        }
    }
}

// Consume the remainder of an already-open block.
fn consume_open_block<'a, I: Iterator<Item = &'a str>>(lines: &mut std::iter::Peekable<I>) {
    let mut depth = 1;
    for line in lines.by_ref() {
        for c in strip_comment(line).trim().chars() {
            match c {
                '{' => depth += 1,
                '}' => depth -= 1,
                _ => {}
            }
        }
        if depth <= 0 {
            return;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn doomednums_parse_and_later_entries_override() {
        let info = parse(
            r#"
DoomEdNums
{
    5050 = "Demo"
    9100 = ZombieGuy
    5050 = "DemoOverride"
}
"#,
        );
        assert_eq!(Some("DemoOverride"), info.class_for(5050));
        assert_eq!(Some("ZombieGuy"), info.class_for(9100));
        assert_eq!(None, info.class_for(1));
    }

    #[test]
    fn map_headers_discover_lump_and_title() {
        let info = parse(
            r#"
map MAP01 "Entryway" { next = "MAP02" }
map E1M1 lookup "HUSTR_E1M1"
{
    sky1 = "SKY1"
}
"#,
        );
        assert_eq!(("MAP01".to_string(), "Entryway".to_string()), info.maps[0]);
        assert_eq!(("E1M1".to_string(), "HUSTR_E1M1".to_string()), info.maps[1]);
    }
}
