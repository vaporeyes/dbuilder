// ABOUTME: MODELDEF parsing; first slice of UDB ModeldefParser porting.
// ABOUTME: Captures per-actor model paths, files, skins, scale, and frame indexes.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

#[derive(Clone, Debug, Default, PartialEq)]
pub struct ModelDefEntry {
    pub class_name: String,
    pub path: String,
    /// Model slot index to file name.
    pub models: Vec<(usize, String)>,
    /// Skin slot index to file name.
    pub skins: Vec<(usize, String)>,
    pub scale: (f64, f64, f64),
    /// (sprite, frame char, model slot, frame number)
    pub frame_indexes: Vec<(String, char, usize, i32)>,
}

/// Parse MODELDEF text into entries. Unknown directives are skipped like UDB.
pub fn parse(text: &str) -> Vec<ModelDefEntry> {
    let mut out = Vec::new();
    let mut toks = tokenize(text);
    let mut i = 0usize;

    while i < toks.len() {
        if !toks[i].eq_ignore_ascii_case("model") || i + 1 >= toks.len() {
            i += 1;
            continue;
        }
        let mut entry = ModelDefEntry {
            class_name: toks[i + 1].clone(),
            scale: (1.0, 1.0, 1.0),
            ..ModelDefEntry::default()
        };
        i += 2;
        if toks.get(i).map(String::as_str) != Some("{") {
            continue;
        }
        i += 1;
        while i < toks.len() && toks[i] != "}" {
            match toks[i].to_lowercase().as_str() {
                "path" => {
                    if let Some(v) = toks.get(i + 1) {
                        entry.path = v.clone();
                    }
                    i += 2;
                }
                "model" => {
                    if let (Some(slot), Some(file)) = (toks.get(i + 1), toks.get(i + 2)) {
                        if let Ok(slot) = slot.parse::<usize>() {
                            entry.models.push((slot, file.clone()));
                        }
                    }
                    i += 3;
                }
                "skin" => {
                    if let (Some(slot), Some(file)) = (toks.get(i + 1), toks.get(i + 2)) {
                        if let Ok(slot) = slot.parse::<usize>() {
                            entry.skins.push((slot, file.clone()));
                        }
                    }
                    i += 3;
                }
                "scale" => {
                    let f = |o: usize| toks.get(i + o).and_then(|t| t.parse::<f64>().ok());
                    if let (Some(x), Some(y), Some(z)) = (f(1), f(2), f(3)) {
                        entry.scale = (x, y, z);
                    }
                    i += 4;
                }
                "frameindex" => {
                    if let (Some(sprite), Some(frame), Some(slot), Some(num)) = (
                        toks.get(i + 1),
                        toks.get(i + 2),
                        toks.get(i + 3),
                        toks.get(i + 4),
                    ) {
                        if let (Some(fc), Ok(slot), Ok(num)) = (
                            frame.chars().next(),
                            slot.parse::<usize>(),
                            num.parse::<i32>(),
                        ) {
                            entry.frame_indexes.push((
                                sprite.to_uppercase(),
                                fc.to_ascii_uppercase(),
                                slot,
                                num,
                            ));
                        }
                    }
                    i += 5;
                }
                _ => i += 1,
            }
        }
        i += 1; // closing brace
        out.push(entry);
    }
    out
}

fn tokenize(text: &str) -> Vec<String> {
    let mut toks = Vec::new();
    for line in text.lines() {
        let code = match line.find("//") {
            Some(p) => &line[..p],
            None => line,
        };
        let mut cur = String::new();
        let mut in_str = false;
        for c in code.chars() {
            match c {
                '"' => {
                    if in_str {
                        toks.push(cur.clone());
                        cur.clear();
                    }
                    in_str = !in_str;
                }
                '{' | '}' if !in_str => {
                    if !cur.is_empty() {
                        toks.push(cur.clone());
                        cur.clear();
                    }
                    toks.push(c.to_string());
                }
                c if c.is_whitespace() && !in_str => {
                    if !cur.is_empty() {
                        toks.push(cur.clone());
                        cur.clear();
                    }
                }
                c => cur.push(c),
            }
        }
        if !cur.is_empty() {
            toks.push(cur);
        }
    }
    toks
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_model_entry_with_all_directives() {
        let entries = parse(
            r#"
// comment
Model ZombieGuy
{
    Path "models/zombie"
    Model 0 "zombie.md3"
    Skin 0 "zombie.png"
    Scale 1.0 1.0 1.2
    FrameIndex POSS A 0 0
    FrameIndex POSS B 0 1
    UnknownDirective
}
"#,
        );
        let e = &entries[0];
        assert_eq!("ZombieGuy", e.class_name);
        assert_eq!("models/zombie", e.path);
        assert_eq!(vec![(0, "zombie.md3".to_string())], e.models);
        assert_eq!(vec![(0, "zombie.png".to_string())], e.skins);
        assert_eq!((1.0, 1.0, 1.2), e.scale);
        assert_eq!(("POSS".to_string(), 'A', 0, 0), e.frame_indexes[0]);
        assert_eq!(("POSS".to_string(), 'B', 0, 1), e.frame_indexes[1]);
    }

    #[test]
    fn multiple_entries_and_default_scale() {
        let entries = parse("model A { model 0 \"a.md2\" } model B { }");
        assert_eq!(2, entries.len());
        assert_eq!((1.0, 1.0, 1.0), entries[1].scale);
    }
}
