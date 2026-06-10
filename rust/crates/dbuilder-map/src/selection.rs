// ABOUTME: Selection and marking semantics ported from DBuilder MapSet helpers (UDB MapSet).
// ABOUTME: Per-element-type flag sets with UDB-style invert, propagation, and conversion.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::MapSet;

#[derive(Clone, Debug, Default, PartialEq, Eq)]
pub struct Selection {
    pub vertices: Vec<bool>,
    pub linedefs: Vec<bool>,
    pub sectors: Vec<bool>,
    pub things: Vec<bool>,
}

impl Selection {
    pub fn for_map(map: &MapSet) -> Selection {
        Selection {
            vertices: vec![false; map.vertices.len()],
            linedefs: vec![false; map.linedefs.len()],
            sectors: vec![false; map.sectors.len()],
            things: vec![false; map.things.len()],
        }
    }

    pub fn clear(&mut self) {
        for flags in [
            &mut self.vertices,
            &mut self.linedefs,
            &mut self.sectors,
            &mut self.things,
        ] {
            flags.iter_mut().for_each(|f| *f = false);
        }
    }

    /// UDB-style invert for one element collection.
    pub fn invert_linedefs(&mut self) {
        self.linedefs.iter_mut().for_each(|f| *f = !*f);
    }

    pub fn invert_vertices(&mut self) {
        self.vertices.iter_mut().for_each(|f| *f = !*f);
    }

    pub fn selected_count(&self) -> usize {
        [&self.vertices, &self.linedefs, &self.sectors, &self.things]
            .iter()
            .map(|v| v.iter().filter(|f| **f).count())
            .sum()
    }

    /// Mark the vertices used by selected linedefs (UDB mark propagation).
    pub fn vertices_from_selected_linedefs(&self, map: &MapSet) -> Vec<bool> {
        let mut out = vec![false; map.vertices.len()];
        for (i, l) in map.linedefs.iter().enumerate() {
            if self.linedefs[i] {
                out[l.start] = true;
                out[l.end] = true;
            }
        }
        out
    }

    /// Select linedefs whose both endpoints are selected (UDB selection conversion).
    pub fn linedefs_from_selected_vertices(&self, map: &MapSet) -> Vec<bool> {
        map.linedefs
            .iter()
            .map(|l| self.vertices[l.start] && self.vertices[l.end])
            .collect()
    }

    /// Select sectors all of whose sidedef-referencing linedefs are selected
    /// (UDB sector-from-complete-linedef-set conversion).
    pub fn sectors_from_selected_linedefs(&self, map: &MapSet) -> Vec<bool> {
        let mut all = vec![true; map.sectors.len()];
        let mut any = vec![false; map.sectors.len()];
        for (i, l) in map.linedefs.iter().enumerate() {
            for side in [l.front, l.back].into_iter().flatten() {
                let sector = map.sidedefs[side].sector;
                any[sector] = true;
                if !self.linedefs[i] {
                    all[sector] = false;
                }
            }
        }
        (0..map.sectors.len()).map(|s| any[s] && all[s]).collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{Linedef, Sector, Sidedef, Vertex};
    use dbuilder_geometry::Vector2D;

    fn square_map() -> MapSet {
        let mut m = MapSet::default();
        for (x, y) in [(0.0, 0.0), (64.0, 0.0), (64.0, 64.0), (0.0, 64.0)] {
            m.vertices.push(Vertex {
                position: Vector2D::new(x, y),
            });
        }
        m.sectors.push(Sector {
            height_floor: 0,
            height_ceiling: 128,
            texture_floor: "F".into(),
            texture_ceiling: "C".into(),
            brightness: 160,
            effect: 0,
            tag: 0,
        });
        for i in 0..4usize {
            m.sidedefs.push(Sidedef {
                offset_x: 0,
                offset_y: 0,
                texture_high: "-".into(),
                texture_low: "-".into(),
                texture_mid: "M".into(),
                sector: 0,
            });
            m.linedefs.push(Linedef {
                start: i,
                end: (i + 1) % 4,
                flags: 0,
                action: 0,
                tag: 0,
                front: Some(i),
                back: None,
                args: [0; 5],
            });
        }
        m
    }

    #[test]
    fn invert_and_clear() {
        let map = square_map();
        let mut sel = Selection::for_map(&map);
        sel.linedefs[0] = true;
        sel.invert_linedefs();
        assert_eq!(vec![false, true, true, true], sel.linedefs);
        sel.clear();
        assert_eq!(0, sel.selected_count());
    }

    #[test]
    fn vertices_propagate_from_selected_linedefs() {
        let map = square_map();
        let mut sel = Selection::for_map(&map);
        sel.linedefs[0] = true; // vertices 0 and 1
        assert_eq!(
            vec![true, true, false, false],
            sel.vertices_from_selected_linedefs(&map)
        );
    }

    #[test]
    fn linedefs_convert_from_selected_vertices() {
        let map = square_map();
        let mut sel = Selection::for_map(&map);
        sel.vertices[0] = true;
        sel.vertices[1] = true;
        assert_eq!(
            vec![true, false, false, false],
            sel.linedefs_from_selected_vertices(&map)
        );
    }

    #[test]
    fn sector_selects_only_with_complete_linedef_set() {
        let map = square_map();
        let mut sel = Selection::for_map(&map);
        for i in 0..3 {
            sel.linedefs[i] = true;
        }
        assert_eq!(vec![false], sel.sectors_from_selected_linedefs(&map));
        sel.linedefs[3] = true;
        assert_eq!(vec![true], sel.sectors_from_selected_linedefs(&map));
    }
}
