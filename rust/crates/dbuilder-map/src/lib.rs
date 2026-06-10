// ABOUTME: Rust port of DBuilder.Map, itself ported from UDB Source/Core/Map.
// ABOUTME: First slice: plain map element records and Doom-format MapSet assembly.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use dbuilder_geometry::Vector2D;
use dbuilder_io::map_lumps::{self, DoomLinedef, DoomSector, DoomSidedef, DoomThing};

#[derive(Clone, Debug, PartialEq)]
pub struct Vertex {
    pub position: Vector2D,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Sidedef {
    pub offset_x: i32,
    pub offset_y: i32,
    pub texture_high: String,
    pub texture_low: String,
    pub texture_mid: String,
    pub sector: usize,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Linedef {
    pub start: usize,
    pub end: usize,
    pub flags: u32,
    pub action: i32,
    pub tag: i32,
    pub front: Option<usize>,
    pub back: Option<usize>,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Sector {
    pub height_floor: i32,
    pub height_ceiling: i32,
    pub texture_floor: String,
    pub texture_ceiling: String,
    pub brightness: i32,
    pub effect: i32,
    pub tag: i32,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Thing {
    pub position: Vector2D,
    pub angle_doom: i32,
    pub thing_type: i32,
    pub flags: u32,
}

#[derive(Clone, Debug, Default, PartialEq)]
pub struct MapSet {
    pub vertices: Vec<Vertex>,
    pub linedefs: Vec<Linedef>,
    pub sidedefs: Vec<Sidedef>,
    pub sectors: Vec<Sector>,
    pub things: Vec<Thing>,
}

impl MapSet {
    /// Assemble a MapSet from decoded Doom-format lumps. Mirrors DBuilder's loader rules:
    /// linedefs with out-of-range vertex references or zero length are skipped, and
    /// sidedef references resolve through the original sidedef indices.
    pub fn from_doom_lumps(
        vertexes: &[(i16, i16)],
        things: &[DoomThing],
        linedefs: &[DoomLinedef],
        sidedefs: &[DoomSidedef],
        sectors: &[DoomSector],
    ) -> MapSet {
        let vertices: Vec<Vertex> = vertexes
            .iter()
            .map(|&(x, y)| Vertex {
                position: Vector2D::new(x as f64, y as f64),
            })
            .collect();

        let map_sectors: Vec<Sector> = sectors
            .iter()
            .map(|s| Sector {
                height_floor: s.height_floor as i32,
                height_ceiling: s.height_ceiling as i32,
                texture_floor: s.texture_floor.clone(),
                texture_ceiling: s.texture_ceiling.clone(),
                brightness: s.brightness as i32,
                effect: s.special as i32,
                tag: s.tag as i32,
            })
            .collect();

        let map_sidedefs: Vec<Option<Sidedef>> = sidedefs
            .iter()
            .map(|s| {
                if (s.sector as usize) < map_sectors.len() {
                    Some(Sidedef {
                        offset_x: s.offset_x as i32,
                        offset_y: s.offset_y as i32,
                        texture_high: s.texture_high.clone(),
                        texture_low: s.texture_low.clone(),
                        texture_mid: s.texture_mid.clone(),
                        sector: s.sector as usize,
                    })
                } else {
                    None
                }
            })
            .collect();

        let side_ref = |r: u16| -> Option<usize> {
            if r == map_lumps::NO_SIDEDEF {
                return None;
            }
            let i = r as usize;
            if i < map_sidedefs.len() && map_sidedefs[i].is_some() {
                Some(i)
            } else {
                None
            }
        };

        let map_linedefs: Vec<Linedef> = linedefs
            .iter()
            .filter(|l| {
                let v1 = l.v1 as usize;
                let v2 = l.v2 as usize;
                v1 < vertices.len()
                    && v2 < vertices.len()
                    // Skip zero-length linedefs like UDB (same start and end vertex position).
                    && (v1 != v2 && vertices[v1].position != vertices[v2].position)
            })
            .map(|l| Linedef {
                start: l.v1 as usize,
                end: l.v2 as usize,
                flags: l.flags as u32,
                action: l.action as i32,
                tag: l.tag as i32,
                front: side_ref(l.front),
                back: side_ref(l.back),
            })
            .collect();

        let map_things: Vec<Thing> = things
            .iter()
            .map(|t| Thing {
                position: Vector2D::new(t.x as f64, t.y as f64),
                angle_doom: t.angle as i32,
                thing_type: t.thing_type as i32,
                flags: t.flags as u32,
            })
            .collect();

        MapSet {
            vertices,
            linedefs: map_linedefs,
            sidedefs: map_sidedefs.into_iter().flatten().collect(),
            sectors: map_sectors,
            things: map_things,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sector() -> DoomSector {
        DoomSector {
            height_floor: 0,
            height_ceiling: 128,
            texture_floor: "FLOOR4_8".into(),
            texture_ceiling: "CEIL3_5".into(),
            brightness: 192,
            special: 0,
            tag: 0,
        }
    }

    fn sidedef(sector: u16) -> DoomSidedef {
        DoomSidedef {
            offset_x: 0,
            offset_y: 0,
            texture_high: "-".into(),
            texture_low: "-".into(),
            texture_mid: "STARTAN2".into(),
            sector,
        }
    }

    fn linedef(v1: u16, v2: u16, front: u16, back: u16) -> DoomLinedef {
        DoomLinedef {
            v1,
            v2,
            flags: 1,
            action: 0,
            tag: 0,
            front,
            back,
        }
    }

    #[test]
    fn square_map_assembles_fully() {
        let vx = [(0i16, 0i16), (64, 0), (64, 64), (0, 64)];
        let lines = [
            linedef(0, 1, 0, map_lumps::NO_SIDEDEF),
            linedef(1, 2, 1, map_lumps::NO_SIDEDEF),
            linedef(2, 3, 2, map_lumps::NO_SIDEDEF),
            linedef(3, 0, 3, map_lumps::NO_SIDEDEF),
        ];
        let sides = [sidedef(0), sidedef(0), sidedef(0), sidedef(0)];
        let things = [DoomThing {
            x: 32,
            y: 32,
            angle: 90,
            thing_type: 1,
            flags: 7,
        }];

        let map = MapSet::from_doom_lumps(&vx, &things, &lines, &sides, &[sector()]);
        assert_eq!(4, map.vertices.len());
        assert_eq!(4, map.linedefs.len());
        assert_eq!(4, map.sidedefs.len());
        assert_eq!(1, map.sectors.len());
        assert_eq!(1, map.things.len());
        assert_eq!(Some(0), map.linedefs[0].front);
        assert_eq!(None, map.linedefs[0].back);
        assert_eq!(0, map.sidedefs[0].sector);
    }

    #[test]
    fn invalid_and_zero_length_linedefs_are_skipped() {
        let vx = [(0i16, 0i16), (64, 0), (0, 0)];
        let lines = [
            linedef(0, 1, map_lumps::NO_SIDEDEF, map_lumps::NO_SIDEDEF), // valid
            linedef(0, 0, map_lumps::NO_SIDEDEF, map_lumps::NO_SIDEDEF), // same vertex
            linedef(0, 2, map_lumps::NO_SIDEDEF, map_lumps::NO_SIDEDEF), // zero length (same position)
            linedef(0, 9, map_lumps::NO_SIDEDEF, map_lumps::NO_SIDEDEF), // out of range
        ];
        let map = MapSet::from_doom_lumps(&vx, &[], &lines, &[], &[]);
        assert_eq!(1, map.linedefs.len());
    }

    #[test]
    fn dangling_sidedef_references_drop_to_none() {
        let vx = [(0i16, 0i16), (64, 0)];
        let lines = [linedef(0, 1, 5, map_lumps::NO_SIDEDEF)];
        let map = MapSet::from_doom_lumps(&vx, &[], &lines, &[], &[]);
        assert_eq!(None, map.linedefs[0].front);
    }
}

use dbuilder_io::udmf::{UdmfBlock, UdmfDocument, UdmfValue};

fn udmf_f64(b: &UdmfBlock, key: &str, default: f64) -> f64 {
    b.fields
        .iter()
        .find(|(k, _)| k == key)
        .map_or(default, |(_, v)| match v {
            UdmfValue::Float(f) => *f,
            UdmfValue::Int(i) => *i as f64,
            _ => default,
        })
}

fn udmf_i32(b: &UdmfBlock, key: &str, default: i32) -> i32 {
    b.fields
        .iter()
        .find(|(k, _)| k == key)
        .map_or(default, |(_, v)| match v {
            UdmfValue::Int(i) => *i as i32,
            _ => default,
        })
}

fn udmf_str(b: &UdmfBlock, key: &str, default: &str) -> String {
    b.fields
        .iter()
        .find(|(k, _)| k == key)
        .map_or(default.to_string(), |(_, v)| match v {
            UdmfValue::String(s) => s.clone(),
            _ => default.to_string(),
        })
}

impl MapSet {
    /// Assemble a MapSet from a parsed UDMF document. Core fields only for now;
    /// custom fields and UDMF-only flags land with the element behavior slices.
    /// Sidedef sector references and linedef vertex/side references resolve by
    /// block order; invalid references are skipped like the C# loader.
    pub fn from_udmf(doc: &UdmfDocument) -> MapSet {
        let mut map = MapSet::default();

        for b in doc.blocks.iter().filter(|b| b.name == "vertex") {
            map.vertices.push(Vertex {
                position: dbuilder_geometry::Vector2D::new(
                    udmf_f64(b, "x", 0.0),
                    udmf_f64(b, "y", 0.0),
                ),
            });
        }
        for b in doc.blocks.iter().filter(|b| b.name == "sector") {
            map.sectors.push(Sector {
                height_floor: udmf_i32(b, "heightfloor", 0),
                height_ceiling: udmf_i32(b, "heightceiling", 0),
                texture_floor: udmf_str(b, "texturefloor", "-"),
                texture_ceiling: udmf_str(b, "textureceiling", "-"),
                brightness: udmf_i32(b, "lightlevel", 160),
                effect: udmf_i32(b, "special", 0),
                tag: udmf_i32(b, "id", 0),
            });
        }
        for b in doc.blocks.iter().filter(|b| b.name == "sidedef") {
            let sector = udmf_i32(b, "sector", -1);
            if sector >= 0 && (sector as usize) < map.sectors.len() {
                map.sidedefs.push(Sidedef {
                    offset_x: udmf_i32(b, "offsetx", 0),
                    offset_y: udmf_i32(b, "offsety", 0),
                    texture_high: udmf_str(b, "texturetop", "-"),
                    texture_low: udmf_str(b, "texturebottom", "-"),
                    texture_mid: udmf_str(b, "texturemiddle", "-"),
                    sector: sector as usize,
                });
            }
        }
        for b in doc.blocks.iter().filter(|b| b.name == "linedef") {
            let v1 = udmf_i32(b, "v1", -1);
            let v2 = udmf_i32(b, "v2", -1);
            let valid = v1 >= 0
                && v2 >= 0
                && (v1 as usize) < map.vertices.len()
                && (v2 as usize) < map.vertices.len()
                && v1 != v2
                && map.vertices[v1 as usize].position != map.vertices[v2 as usize].position;
            if !valid {
                continue;
            }
            let side = |key: &str| -> Option<usize> {
                let s = udmf_i32(b, key, -1);
                if s >= 0 && (s as usize) < map.sidedefs.len() {
                    Some(s as usize)
                } else {
                    None
                }
            };
            map.linedefs.push(Linedef {
                start: v1 as usize,
                end: v2 as usize,
                flags: 0,
                action: udmf_i32(b, "special", 0),
                tag: udmf_i32(b, "id", 0),
                front: side("sidefront"),
                back: side("sideback"),
            });
        }
        for b in doc.blocks.iter().filter(|b| b.name == "thing") {
            map.things.push(Thing {
                position: dbuilder_geometry::Vector2D::new(
                    udmf_f64(b, "x", 0.0),
                    udmf_f64(b, "y", 0.0),
                ),
                angle_doom: udmf_i32(b, "angle", 0),
                thing_type: udmf_i32(b, "type", 0),
                flags: 0,
            });
        }

        map
    }
}

#[cfg(test)]
mod udmf_tests {
    use super::*;
    use dbuilder_io::udmf;

    #[test]
    fn assembles_textmap_square() {
        let text = r#"
namespace = "zdoom";
vertex { x = 0.0; y = 0.0; }
vertex { x = 64.0; y = 0.0; }
vertex { x = 64.0; y = 64.0; }
vertex { x = 0.0; y = 64.0; }
sector { heightceiling = 128; texturefloor = "FLOOR4_8"; textureceiling = "CEIL3_5"; lightlevel = 192; }
sidedef { sector = 0; texturemiddle = "STARTAN2"; }
linedef { v1 = 0; v2 = 1; sidefront = 0; }
linedef { v1 = 1; v2 = 2; sidefront = 0; }
linedef { v1 = 2; v2 = 3; sidefront = 0; }
linedef { v1 = 3; v2 = 0; sidefront = 0; }
thing { x = 32.0; y = 32.0; angle = 90; type = 1; }
"#;
        let doc = udmf::parse(text).unwrap();
        let map = MapSet::from_udmf(&doc);
        assert_eq!(4, map.vertices.len());
        assert_eq!(4, map.linedefs.len());
        assert_eq!(1, map.sidedefs.len());
        assert_eq!(1, map.sectors.len());
        assert_eq!(1, map.things.len());
        assert_eq!(Some(0), map.linedefs[0].front);
        assert_eq!(None, map.linedefs[0].back);
        assert_eq!(192, map.sectors[0].brightness);
        assert_eq!("STARTAN2", map.sidedefs[0].texture_mid);
    }

    #[test]
    fn invalid_udmf_linedefs_are_skipped() {
        let text = r#"
namespace = "zdoom";
vertex { x = 0.0; y = 0.0; }
vertex { x = 0.0; y = 0.0; }
vertex { x = 64.0; y = 0.0; }
linedef { v1 = 0; v2 = 2; }
linedef { v1 = 0; v2 = 1; }
linedef { v1 = 0; v2 = 9; }
"#;
        let map = MapSet::from_udmf(&udmf::parse(text).unwrap());
        assert_eq!(1, map.linedefs.len());
    }
}
