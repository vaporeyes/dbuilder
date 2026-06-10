// ABOUTME: DECORATE actor discovery; first slice of UDB DecorateParser porting.
// ABOUTME: Parses actor headers, editor-key comments, scalar properties, and flags.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

#[derive(Clone, Debug, Default, PartialEq)]
pub struct DecorateActor {
    pub name: String,
    pub parent: Option<String>,
    pub replaces: Option<String>,
    pub doomednum: Option<i32>,
    pub radius: Option<f64>,
    pub height: Option<f64>,
    pub flags: Vec<(String, bool)>,
    /// //$Key value editor comments inside the actor body, keys lowercased.
    pub editor_keys: Vec<(String, String)>,
}

/// Parse DECORATE text into discovered actors. States blocks are skipped for
/// now; header inheritance, replaces, doomednum, scalar properties, +/- flags,
/// and //$ editor keys are captured like the C# parser's discovery pass.
pub fn parse(text: &str) -> Vec<DecorateActor> {
    let mut actors = Vec::new();
    let mut lines = text.lines().peekable();

    while let Some(line) = lines.next() {
        let trimmed = strip_comment(line).trim().to_string();
        let lower = trimmed.to_lowercase();
        if !lower.starts_with("actor ") && lower != "actor" {
            continue;
        }

        let mut actor = DecorateActor::default();
        let after_keyword = trimmed[5..].trim();

        // The opening brace (and even a full body) may share the header line.
        let (header, inline_rest) = match after_keyword.find('{') {
            Some(i) => (&after_keyword[..i], Some(&after_keyword[i..])),
            None => (after_keyword, None),
        };
        parse_header(header.trim(), &mut actor);

        let mut depth;
        if let Some(rest) = inline_rest {
            depth = line_brace_delta(rest);
            for part in rest.trim_matches(|c| c == '{' || c == '}').split(';') {
                parse_body_line(part.trim(), &mut actor);
            }
            if depth <= 0 {
                actors.push(actor);
                continue;
            }
        } else {
            // Scan to the opening brace on a later line.
            depth = 0;
            while depth == 0 {
                let Some(next) = lines.next() else {
                    return actors;
                };
                depth = line_brace_delta(strip_comment(next).trim());
            }
        }

        // Body: collect until the matching close brace, skipping nested blocks
        // (states, etc.) for properties but still reading editor keys anywhere.
        for body_line in lines.by_ref() {
            let raw = body_line.trim();
            if let Some(key) = raw.strip_prefix("//$") {
                if let Some((k, v)) = key.split_once(char::is_whitespace) {
                    actor
                        .editor_keys
                        .push((k.to_lowercase(), v.trim().to_string()));
                }
            }
            let code = strip_comment(body_line).trim().to_string();
            let delta = line_brace_delta(&code);
            if depth == 1 && delta <= 0 {
                parse_body_line(&code, &mut actor);
            }
            depth += delta;
            if depth <= 0 {
                break;
            }
        }
        actors.push(actor);
    }

    actors
}

fn strip_comment(line: &str) -> &str {
    match line.find("//") {
        Some(i) => &line[..i],
        None => line,
    }
}

fn line_brace_delta(line: &str) -> i32 {
    line.chars().fold(0, |d, c| match c {
        '{' => d + 1,
        '}' => d - 1,
        _ => d,
    })
}

fn parse_header(header: &str, actor: &mut DecorateActor) {
    // Forms: Name [: Parent] [replaces Other] [doomednum]
    let mut rest = header.to_string();
    if let Some((name_part, parent_part)) = rest.clone().split_once(':') {
        actor.name = name_part.trim().to_string();
        rest = parent_part.trim().to_string();
        let mut toks = rest.split_whitespace();
        actor.parent = toks.next().map(str::to_string);
        rest = toks.collect::<Vec<_>>().join(" ");
    } else {
        let mut toks = rest.split_whitespace();
        actor.name = toks.next().unwrap_or("").to_string();
        rest = toks.collect::<Vec<_>>().join(" ");
    }

    let mut toks = rest.split_whitespace().peekable();
    while let Some(tok) = toks.next() {
        if tok.eq_ignore_ascii_case("replaces") {
            actor.replaces = toks.next().map(str::to_string);
        } else if let Ok(num) = tok.parse::<i32>() {
            actor.doomednum = Some(num);
        }
    }
}

fn parse_body_line(code: &str, actor: &mut DecorateActor) {
    if code.is_empty() {
        return;
    }
    if let Some(flag) = code.strip_prefix('+') {
        actor.flags.push((flag.trim().to_lowercase(), true));
        return;
    }
    if let Some(flag) = code.strip_prefix('-') {
        actor.flags.push((flag.trim().to_lowercase(), false));
        return;
    }
    let mut toks = code.split_whitespace();
    let Some(key) = toks.next() else { return };
    let value = toks.next().unwrap_or("");
    match key.to_lowercase().as_str() {
        "radius" => actor.radius = value.parse().ok(),
        "height" => actor.height = value.parse().ok(),
        _ => {}
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const TEXT: &str = r#"
// A monster
ACTOR ZombieGuy : ZombieMan replaces DoomImp 12001
{
    //$Category Monsters
    //$Title Zombie Guy
    Radius 20
    Height 56
    +SOLID
    -COUNTKILL
    States
    {
    Spawn:
        POSS AB 10 A_Look
        Loop
    }
}

actor SimpleThing 5000 { Radius 8 }
"#;

    #[test]
    fn parses_header_inheritance_and_doomednum() {
        let actors = parse(TEXT);
        assert_eq!(2, actors.len());
        let z = &actors[0];
        assert_eq!("ZombieGuy", z.name);
        assert_eq!(Some("ZombieMan".to_string()), z.parent);
        assert_eq!(Some("DoomImp".to_string()), z.replaces);
        assert_eq!(Some(12001), z.doomednum);
    }

    #[test]
    fn parses_properties_flags_and_editor_keys() {
        let z = &parse(TEXT)[0];
        assert_eq!(Some(20.0), z.radius);
        assert_eq!(Some(56.0), z.height);
        assert!(z.flags.contains(&("solid".to_string(), true)));
        assert!(z.flags.contains(&("countkill".to_string(), false)));
        assert!(z
            .editor_keys
            .contains(&("category".to_string(), "Monsters".to_string())));
        assert!(z
            .editor_keys
            .contains(&("title".to_string(), "Zombie Guy".to_string())));
    }

    #[test]
    fn inline_braces_and_states_blocks_do_not_leak() {
        let actors = parse(TEXT);
        let s = &actors[1];
        assert_eq!("SimpleThing", s.name);
        assert_eq!(Some(5000), s.doomednum);
        assert_eq!(Some(8.0), s.radius);
        // No states content parsed as properties.
        assert!(actors[0].flags.len() == 2);
    }
}
