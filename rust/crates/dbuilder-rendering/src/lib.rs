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

/// Build grid lines covering the visible world area at the given spacing,
/// clamped like UDB's dynamic grid so zoomed-out views stay usable.
pub fn build_grid_batch(
    view: &View2D,
    screen_width: f64,
    screen_height: f64,
    spacing: f64,
    color: u32,
) -> Vec<LineVertex> {
    let mut out = Vec::new();
    if spacing <= 0.0 || view.scale <= 0.0 {
        return out;
    }
    let tl = view.screen_to_world(Vector2D::new(0.0, 0.0), screen_height);
    let br = view.screen_to_world(Vector2D::new(screen_width, screen_height), screen_height);
    let (x0, x1) = (br.x.min(tl.x), br.x.max(tl.x));
    let (y0, y1) = (br.y.min(tl.y), br.y.max(tl.y));

    let mut x = (x0 / spacing).floor() * spacing;
    while x <= x1 {
        let a = view.world_to_screen(Vector2D::new(x, y0), screen_height);
        let b = view.world_to_screen(Vector2D::new(x, y1), screen_height);
        out.push(LineVertex {
            x: a.x as f32,
            y: a.y as f32,
            color,
        });
        out.push(LineVertex {
            x: b.x as f32,
            y: b.y as f32,
            color,
        });
        x += spacing;
    }
    let mut y = (y0 / spacing).floor() * spacing;
    while y <= y1 {
        let a = view.world_to_screen(Vector2D::new(x0, y), screen_height);
        let b = view.world_to_screen(Vector2D::new(x1, y), screen_height);
        out.push(LineVertex {
            x: a.x as f32,
            y: a.y as f32,
            color,
        });
        out.push(LineVertex {
            x: b.x as f32,
            y: b.y as f32,
            color,
        });
        y += spacing;
    }
    out
}

/// Build thing markers: a screen-space square outline per thing plus a facing
/// tick from center toward the thing's Doom angle, like UDB's 2D thing display.
pub fn build_thing_batch(
    map: &MapSet,
    view: &View2D,
    screen_height: f64,
    half_size: f64,
    color: &dyn Fn(usize) -> u32,
) -> Vec<LineVertex> {
    let mut out = Vec::new();
    for (i, t) in map.things.iter().enumerate() {
        let c = color(i);
        let center = view.world_to_screen(t.position, screen_height);
        let h = half_size as f32;
        let (cx, cy) = (center.x as f32, center.y as f32);
        let corners = [
            (cx - h, cy - h),
            (cx + h, cy - h),
            (cx + h, cy + h),
            (cx - h, cy + h),
        ];
        for e in 0..4 {
            let (ax, ay) = corners[e];
            let (bx, by) = corners[(e + 1) % 4];
            out.push(LineVertex {
                x: ax,
                y: ay,
                color: c,
            });
            out.push(LineVertex {
                x: bx,
                y: by,
                color: c,
            });
        }
        // Facing tick: doom angle 0 = east, 90 = north (screen Y down).
        let rad = (t.angle_doom as f64).to_radians();
        out.push(LineVertex {
            x: cx,
            y: cy,
            color: c,
        });
        out.push(LineVertex {
            x: cx + (rad.cos() * half_size) as f32,
            y: cy - (rad.sin() * half_size) as f32,
            color: c,
        });
    }
    out
}

#[cfg(test)]
mod batch_tests {
    use super::*;
    use dbuilder_map::Thing;

    const H: f64 = 600.0;

    #[test]
    fn grid_batch_covers_view_and_rejects_bad_spacing() {
        let view = View2D::default();
        let batch = build_grid_batch(&view, 800.0, H, 64.0, 0xff333333);
        // 800/64 -> at least 13 vertical and 600/64 -> at least 10 horizontal lines.
        assert!(batch.len() >= (13 + 10) * 2);
        assert!(build_grid_batch(&view, 800.0, H, 0.0, 0).is_empty());
    }

    #[test]
    fn thing_batch_emits_square_and_facing_tick() {
        let mut map = MapSet::default();
        map.things.push(Thing {
            position: Vector2D::new(0.0, 0.0),
            angle_doom: 90,
            thing_type: 1,
            flags: 0,
            tid: 0,
            z: 0.0,
            special: 0,
            args: [0; 5],
        });
        let batch = build_thing_batch(&map, &View2D::default(), H, 8.0, &|_| 1);
        assert_eq!(10, batch.len()); // 4 edges * 2 + tick * 2
                                     // Angle 90 (north) points up on screen: tick end has smaller y.
        assert!(batch[9].y < batch[8].y);
        assert!((batch[9].x - batch[8].x).abs() < 1e-4);
    }
}
