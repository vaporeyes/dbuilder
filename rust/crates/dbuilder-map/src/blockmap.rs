// ABOUTME: Editor blockmap ported from DBuilder BlockMap behavior (UDB BlockMap<T>).
// ABOUTME: 128-unit cells; linedefs bucket into crossed cells, not bounding boxes.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::MapSet;
use dbuilder_geometry::{Line2D, RectF, Vector2D};
use std::collections::HashMap;

pub const BLOCK_SIZE: i32 = 128;

#[derive(Clone, Debug, Default, PartialEq)]
pub struct BlockEntry {
    pub linedefs: Vec<usize>,
    pub things: Vec<usize>,
    pub vertices: Vec<usize>,
}

#[derive(Clone, Debug, Default)]
pub struct BlockMap {
    blocks: HashMap<(i32, i32), BlockEntry>,
}

fn block_coord(v: f64) -> i32 {
    (v / BLOCK_SIZE as f64).floor() as i32
}

impl BlockMap {
    pub fn new() -> BlockMap {
        BlockMap::default()
    }

    pub fn block_at(&self, p: Vector2D) -> Option<&BlockEntry> {
        self.blocks.get(&(block_coord(p.x), block_coord(p.y)))
    }

    pub fn add_things(&mut self, map: &MapSet) {
        for (i, t) in map.things.iter().enumerate() {
            let key = (block_coord(t.position.x), block_coord(t.position.y));
            self.blocks.entry(key).or_default().things.push(i);
        }
    }

    pub fn add_vertices(&mut self, map: &MapSet) {
        for (i, v) in map.vertices.iter().enumerate() {
            let key = (block_coord(v.position.x), block_coord(v.position.y));
            self.blocks.entry(key).or_default().vertices.push(i);
        }
    }

    /// Bucket linedefs into every cell the segment actually crosses (tested by
    /// rectangle clipping), matching DBuilder's crossed-cells behavior rather
    /// than full bounding boxes.
    pub fn add_linedefs(&mut self, map: &MapSet) {
        for (i, l) in map.linedefs.iter().enumerate() {
            let a = map.vertices[l.start].position;
            let b = map.vertices[l.end].position;
            let line = Line2D::new(a, b);

            let (bx0, bx1) = (block_coord(a.x.min(b.x)), block_coord(a.x.max(b.x)));
            let (by0, by1) = (block_coord(a.y.min(b.y)), block_coord(a.y.max(b.y)));

            for bx in bx0..=bx1 {
                for by in by0..=by1 {
                    let rect = RectF::from_ltwh(
                        (bx * BLOCK_SIZE) as f32,
                        (by * BLOCK_SIZE) as f32,
                        BLOCK_SIZE as f32,
                        BLOCK_SIZE as f32,
                    );
                    let endpoint_inside = |p: Vector2D| {
                        p.x >= rect.left as f64
                            && p.x <= rect.right as f64
                            && p.y >= rect.top as f64
                            && p.y <= rect.bottom as f64
                    };
                    if endpoint_inside(a)
                        || endpoint_inside(b)
                        || Line2D::clip_to_rectangle(line, rect).is_some()
                    {
                        self.blocks.entry((bx, by)).or_default().linedefs.push(i);
                    }
                }
            }
        }
    }

    /// All entries within the square world-range around a point (UDB-style range query).
    pub fn get_square_range(&self, center: Vector2D, range: f64) -> Vec<&BlockEntry> {
        let (bx0, bx1) = (block_coord(center.x - range), block_coord(center.x + range));
        let (by0, by1) = (block_coord(center.y - range), block_coord(center.y + range));
        let mut out = Vec::new();
        for bx in bx0..=bx1 {
            for by in by0..=by1 {
                if let Some(e) = self.blocks.get(&(bx, by)) {
                    out.push(e);
                }
            }
        }
        out
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{Thing, Vertex};

    fn map_with(
        vertices: &[(f64, f64)],
        lines: &[(usize, usize)],
        things: &[(f64, f64)],
    ) -> MapSet {
        let mut m = MapSet::default();
        for &(x, y) in vertices {
            m.vertices.push(Vertex {
                position: Vector2D::new(x, y),
            });
        }
        for &(s, e) in lines {
            m.linedefs.push(crate::Linedef {
                start: s,
                end: e,
                flags: 0,
                action: 0,
                tag: 0,
                front: None,
                back: None,
                args: [0; 5],
            });
        }
        for &(x, y) in things {
            m.things.push(Thing {
                position: Vector2D::new(x, y),
                angle_doom: 0,
                thing_type: 1,
                flags: 0,
                tid: 0,
                z: 0.0,
                special: 0,
                args: [0; 5],
            });
        }
        m
    }

    #[test]
    fn things_bucket_by_position() {
        let map = map_with(&[], &[], &[(32.0, 32.0), (200.0, 32.0)]);
        let mut bm = BlockMap::new();
        bm.add_things(&map);
        assert_eq!(
            vec![0],
            bm.block_at(Vector2D::new(0.0, 0.0)).unwrap().things
        );
        assert_eq!(
            vec![1],
            bm.block_at(Vector2D::new(200.0, 0.0)).unwrap().things
        );
    }

    #[test]
    fn diagonal_linedef_skips_uncrossed_corner_cells() {
        // Diagonal from (10,10) to (240,240) spans a 2x2 cell bbox but only
        // crosses the two diagonal cells, not the off-diagonal corners.
        let map = map_with(&[(10.0, 10.0), (240.0, 240.0)], &[(0, 1)], &[]);
        let mut bm = BlockMap::new();
        bm.add_linedefs(&map);
        assert!(bm.block_at(Vector2D::new(64.0, 64.0)).is_some());
        assert!(bm.block_at(Vector2D::new(200.0, 200.0)).is_some());
        assert!(bm.block_at(Vector2D::new(200.0, 64.0)).is_none());
        assert!(bm.block_at(Vector2D::new(64.0, 200.0)).is_none());
    }

    #[test]
    fn square_range_collects_neighboring_blocks() {
        let map = map_with(&[], &[], &[(32.0, 32.0), (200.0, 32.0), (1000.0, 1000.0)]);
        let mut bm = BlockMap::new();
        bm.add_things(&map);
        let found: Vec<usize> = bm
            .get_square_range(Vector2D::new(100.0, 32.0), 128.0)
            .iter()
            .flat_map(|e| e.things.iter().copied())
            .collect();
        assert!(found.contains(&0) && found.contains(&1) && !found.contains(&2));
    }
}
