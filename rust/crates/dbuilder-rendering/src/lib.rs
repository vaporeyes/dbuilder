// ABOUTME: Rust port of DBuilder.Rendering, itself replacing UDB's renderer.
// ABOUTME: First slice: 2D view transform math and linedef batch building, GPU-free.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use dbuilder_geometry::Vector2D;
use dbuilder_map::MapSet;

/// 2D map view: world offset and zoom scale, mirroring UDB Renderer2D's
/// offsetx/offsety/scale presentation state.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct View2D {
    pub offset: Vector2D,
    pub scale: f64,
}

impl Default for View2D {
    fn default() -> View2D {
        View2D {
            offset: Vector2D::new(0.0, 0.0),
            scale: 1.0,
        }
    }
}

impl View2D {
    /// World to screen: translate then scale; Y flips because screen Y grows down.
    pub fn world_to_screen(&self, w: Vector2D, screen_height: f64) -> Vector2D {
        Vector2D::new(
            (w.x - self.offset.x) * self.scale,
            screen_height - (w.y - self.offset.y) * self.scale,
        )
    }

    pub fn screen_to_world(&self, s: Vector2D, screen_height: f64) -> Vector2D {
        Vector2D::new(
            s.x / self.scale + self.offset.x,
            (screen_height - s.y) / self.scale + self.offset.y,
        )
    }

    /// Zoom by a factor while keeping the world point under the cursor fixed,
    /// like UDB's zoom-toward-mouse behavior.
    pub fn zoom_at(&mut self, cursor_screen: Vector2D, screen_height: f64, factor: f64) {
        let before = self.screen_to_world(cursor_screen, screen_height);
        self.scale *= factor;
        let after = self.screen_to_world(cursor_screen, screen_height);
        self.offset = self.offset + (before - after);
    }
}

/// One colored 2D line vertex: x, y in screen space plus ARGB color.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct LineVertex {
    pub x: f32,
    pub y: f32,
    pub color: u32,
}

/// Build a line-list vertex batch for all linedefs (two vertices per line),
/// colored per line by the callback (selection, action, block colors, etc.).
pub fn build_linedef_batch(
    map: &MapSet,
    view: &View2D,
    screen_height: f64,
    color: &dyn Fn(usize) -> u32,
) -> Vec<LineVertex> {
    let mut out = Vec::with_capacity(map.linedefs.len() * 2);
    for (i, l) in map.linedefs.iter().enumerate() {
        let c = color(i);
        for v in [l.start, l.end] {
            let s = view.world_to_screen(map.vertices[v].position, screen_height);
            out.push(LineVertex {
                x: s.x as f32,
                y: s.y as f32,
                color: c,
            });
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use dbuilder_map::{Linedef, Vertex};

    const H: f64 = 600.0;

    #[test]
    fn world_screen_round_trips() {
        let view = View2D {
            offset: Vector2D::new(-100.0, 50.0),
            scale: 2.0,
        };
        let w = Vector2D::new(32.0, -64.0);
        let s = view.world_to_screen(w, H);
        let back = view.screen_to_world(s, H);
        assert!((back.x - w.x).abs() < 1e-9 && (back.y - w.y).abs() < 1e-9);
    }

    #[test]
    fn zoom_at_keeps_cursor_world_position() {
        let mut view = View2D::default();
        let cursor = Vector2D::new(400.0, 300.0);
        let before = view.screen_to_world(cursor, H);
        view.zoom_at(cursor, H, 2.0);
        let after = view.screen_to_world(cursor, H);
        assert!((before.x - after.x).abs() < 1e-9 && (before.y - after.y).abs() < 1e-9);
        assert_eq!(2.0, view.scale);
    }

    #[test]
    fn linedef_batch_emits_two_colored_vertices_per_line() {
        let mut map = MapSet::default();
        map.vertices.push(Vertex {
            position: Vector2D::new(0.0, 0.0),
        });
        map.vertices.push(Vertex {
            position: Vector2D::new(64.0, 0.0),
        });
        map.linedefs.push(Linedef {
            start: 0,
            end: 1,
            flags: 0,
            action: 0,
            tag: 0,
            front: None,
            back: None,
            args: [0; 5],
        });
        let batch = build_linedef_batch(&map, &View2D::default(), H, &|_| 0xffff0000);
        assert_eq!(2, batch.len());
        assert_eq!(0xffff0000, batch[0].color);
        assert_eq!((0.0, 600.0), (batch[0].x, batch[0].y));
        assert_eq!((64.0, 600.0), (batch[1].x, batch[1].y));
    }
}
