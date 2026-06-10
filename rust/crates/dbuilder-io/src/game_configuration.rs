// ABOUTME: Typed game configuration projected from parsed config structures.
// ABOUTME: First slice of GameConfiguration.cs: identity, format, things, actions.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::config::ConfigValue;

#[derive(Clone, Debug, Default, PartialEq)]
pub struct ThingTypeInfo {
    pub doomednum: i32,
    pub title: String,
    pub width: f64,
    pub height: f64,
    pub hangs: bool,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct ThingCategory {
    pub name: String,
    pub title: String,
    pub things: Vec<ThingTypeInfo>,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct LinedefActionInfo {
    pub index: i32,
    pub title: String,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct GameConfiguration {
    pub name: String,
    pub format_interface: String,
    pub default_lump_name: String,
    pub default_texture: String,
    pub sky_flat: String,
    pub thing_categories: Vec<ThingCategory>,
    pub linedef_actions: Vec<LinedefActionInfo>,
}

fn cfg_str(root: &ConfigValue, path: &str, default: &str) -> String {
    root.lookup(path)
        .and_then(|v| v.as_str())
        .unwrap_or(default)
        .to_string()
}

fn cfg_f64(v: &ConfigValue, key: &str, default: f64) -> f64 {
    match v.lookup(key) {
        Some(ConfigValue::Float(f)) => *f,
        Some(ConfigValue::Int(i)) => *i as f64,
        _ => default,
    }
}

impl GameConfiguration {
    /// Project a parsed configuration into typed metadata. Category defaults
    /// (width/height/hangs) cascade onto things that do not override them,
    /// matching UDB's thing category default inheritance.
    pub fn from_config(root: &ConfigValue) -> GameConfiguration {
        let mut gc = GameConfiguration {
            name: cfg_str(root, "game", ""),
            format_interface: cfg_str(root, "formatinterface", ""),
            default_lump_name: cfg_str(root, "defaultlumpname", ""),
            default_texture: cfg_str(root, "defaulttexture", "-"),
            sky_flat: cfg_str(root, "skyflatname", "F_SKY1"),
            ..GameConfiguration::default()
        };

        if let Some(ConfigValue::Struct(cats)) = root.lookup("thingtypes") {
            for (cat_name, cat_val) in cats {
                let ConfigValue::Struct(entries) = cat_val else {
                    continue;
                };
                let mut category = ThingCategory {
                    name: cat_name.clone(),
                    title: cat_val
                        .lookup("title")
                        .and_then(|v| v.as_str())
                        .unwrap_or(cat_name)
                        .to_string(),
                    things: Vec::new(),
                };
                let def_width = cfg_f64(cat_val, "width", 16.0);
                let def_height = cfg_f64(cat_val, "height", 16.0);
                let def_hangs = cat_val
                    .lookup("hangs")
                    .and_then(|v| v.as_i64())
                    .unwrap_or(0)
                    != 0;

                for (key, val) in entries {
                    let Ok(doomednum) = key.parse::<i32>() else {
                        continue;
                    };
                    let thing = match val {
                        // Compact form: 3001 = "Imp";
                        ConfigValue::String(title) => ThingTypeInfo {
                            doomednum,
                            title: title.clone(),
                            width: def_width,
                            height: def_height,
                            hangs: def_hangs,
                        },
                        // Full form: 3001 { title = "Imp"; width = 20; }
                        ConfigValue::Struct(_) => ThingTypeInfo {
                            doomednum,
                            title: val
                                .lookup("title")
                                .and_then(|v| v.as_str())
                                .unwrap_or("")
                                .to_string(),
                            width: cfg_f64(val, "width", def_width),
                            height: cfg_f64(val, "height", def_height),
                            hangs: val
                                .lookup("hangs")
                                .and_then(|v| v.as_i64())
                                .map(|i| i != 0)
                                .unwrap_or(def_hangs),
                        },
                        _ => continue,
                    };
                    category.things.push(thing);
                }
                gc.thing_categories.push(category);
            }
        }

        if let Some(ConfigValue::Struct(sections)) = root.lookup("linedeftypes") {
            for (_, section) in sections {
                let ConfigValue::Struct(actions) = section else {
                    continue;
                };
                for (key, val) in actions {
                    let Ok(index) = key.parse::<i32>() else {
                        continue;
                    };
                    let title = match val {
                        ConfigValue::String(s) => s.clone(),
                        ConfigValue::Struct(_) => val
                            .lookup("title")
                            .and_then(|v| v.as_str())
                            .unwrap_or("")
                            .to_string(),
                        _ => continue,
                    };
                    gc.linedef_actions.push(LinedefActionInfo { index, title });
                }
            }
        }

        gc
    }

    pub fn thing_title(&self, doomednum: i32) -> Option<&str> {
        self.thing_categories
            .iter()
            .flat_map(|c| &c.things)
            .find(|t| t.doomednum == doomednum)
            .map(|t| t.title.as_str())
    }

    pub fn action_title(&self, index: i32) -> Option<&str> {
        self.linedef_actions
            .iter()
            .find(|a| a.index == index)
            .map(|a| a.title.as_str())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config;

    const CFG: &str = r#"
game = "Doom 2";
formatinterface = "DoomMapSetIO";
defaultlumpname = "MAP01";
thingtypes
{
    monsters
    {
        title = "Monsters";
        width = 20;
        height = 56;
        3001 = "Imp";
        3003 { title = "Baron of Hell"; width = 24; height = 64; }
    }
    decoration
    {
        title = "Decoration";
        63 { title = "Hanging victim"; hangs = 1; }
    }
}
linedeftypes
{
    door
    {
        title = "Door";
        1 = "DR Door open wait close";
        11 { title = "S1 Exit level"; }
    }
}
"#;

    #[test]
    fn projects_identity_and_format() {
        let gc = GameConfiguration::from_config(&config::parse(CFG).unwrap());
        assert_eq!("Doom 2", gc.name);
        assert_eq!("DoomMapSetIO", gc.format_interface);
        assert_eq!("MAP01", gc.default_lump_name);
        assert_eq!("F_SKY1", gc.sky_flat);
    }

    #[test]
    fn thing_categories_cascade_defaults() {
        let gc = GameConfiguration::from_config(&config::parse(CFG).unwrap());
        assert_eq!(Some("Imp"), gc.thing_title(3001));
        let imp = &gc.thing_categories[0].things[0];
        assert_eq!((20.0, 56.0), (imp.width, imp.height));
        let baron = &gc.thing_categories[0].things[1];
        assert_eq!((24.0, 64.0), (baron.width, baron.height));
        let hanging = &gc.thing_categories[1].things[0];
        assert!(hanging.hangs);
    }

    #[test]
    fn linedef_actions_project_from_all_sections() {
        let gc = GameConfiguration::from_config(&config::parse(CFG).unwrap());
        assert_eq!(Some("DR Door open wait close"), gc.action_title(1));
        assert_eq!(Some("S1 Exit level"), gc.action_title(11));
        assert_eq!(None, gc.action_title(999));
    }
}
