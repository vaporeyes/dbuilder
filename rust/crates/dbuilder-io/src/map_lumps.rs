// ABOUTME: Doom-format binary map lump codecs; first slice of UDB MapSetIO porting.
// ABOUTME: VERTEXES entries are little-endian i16 x/y pairs, 4 bytes per vertex.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

/// Read a Doom-format VERTEXES lump. Trailing partial entries are ignored,
/// matching UDB's count = length / 4 read loop.
pub fn read_doom_vertexes(data: &[u8]) -> Vec<(i16, i16)> {
    data.chunks_exact(4)
        .map(|c| {
            (
                i16::from_le_bytes([c[0], c[1]]),
                i16::from_le_bytes([c[2], c[3]]),
            )
        })
        .collect()
}

/// Write a Doom-format VERTEXES lump.
pub fn write_doom_vertexes(vertexes: &[(i16, i16)]) -> Vec<u8> {
    let mut out = Vec::with_capacity(vertexes.len() * 4);
    for &(x, y) in vertexes {
        out.extend_from_slice(&x.to_le_bytes());
        out.extend_from_slice(&y.to_le_bytes());
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn vertexes_round_trip() {
        let v = [(0i16, 0i16), (-32768, 32767), (128, -64)];
        let bytes = write_doom_vertexes(&v);
        assert_eq!(12, bytes.len());
        assert_eq!(v.to_vec(), read_doom_vertexes(&bytes));
    }

    #[test]
    fn trailing_partial_entry_is_ignored() {
        let mut bytes = write_doom_vertexes(&[(1, 2)]);
        bytes.push(0xFF);
        assert_eq!(vec![(1, 2)], read_doom_vertexes(&bytes));
    }
}

use crate::lump_name;

fn u16le(c: &[u8], i: usize) -> u16 {
    u16::from_le_bytes([c[i], c[i + 1]])
}
fn i16le(c: &[u8], i: usize) -> i16 {
    i16::from_le_bytes([c[i], c[i + 1]])
}

// Doom THINGS entry: 10 bytes. Flags stay unsigned above signed-short range like DBuilder.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DoomThing {
    pub x: i16,
    pub y: i16,
    pub angle: i16,
    pub thing_type: u16,
    pub flags: u16,
}

pub fn read_doom_things(data: &[u8]) -> Vec<DoomThing> {
    data.chunks_exact(10)
        .map(|c| DoomThing {
            x: i16le(c, 0),
            y: i16le(c, 2),
            angle: i16le(c, 4),
            thing_type: u16le(c, 6),
            flags: u16le(c, 8),
        })
        .collect()
}

pub fn write_doom_things(things: &[DoomThing]) -> Vec<u8> {
    let mut out = Vec::with_capacity(things.len() * 10);
    for t in things {
        out.extend_from_slice(&t.x.to_le_bytes());
        out.extend_from_slice(&t.y.to_le_bytes());
        out.extend_from_slice(&t.angle.to_le_bytes());
        out.extend_from_slice(&t.thing_type.to_le_bytes());
        out.extend_from_slice(&t.flags.to_le_bytes());
    }
    out
}

// Doom LINEDEFS entry: 14 bytes. 0xFFFF sidedef references mean "none".
pub const NO_SIDEDEF: u16 = 0xFFFF;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DoomLinedef {
    pub v1: u16,
    pub v2: u16,
    pub flags: u16,
    pub action: u16,
    pub tag: u16,
    pub front: u16,
    pub back: u16,
}

pub fn read_doom_linedefs(data: &[u8]) -> Vec<DoomLinedef> {
    data.chunks_exact(14)
        .map(|c| DoomLinedef {
            v1: u16le(c, 0),
            v2: u16le(c, 2),
            flags: u16le(c, 4),
            action: u16le(c, 6),
            tag: u16le(c, 8),
            front: u16le(c, 10),
            back: u16le(c, 12),
        })
        .collect()
}

pub fn write_doom_linedefs(linedefs: &[DoomLinedef]) -> Vec<u8> {
    let mut out = Vec::with_capacity(linedefs.len() * 14);
    for l in linedefs {
        for v in [l.v1, l.v2, l.flags, l.action, l.tag, l.front, l.back] {
            out.extend_from_slice(&v.to_le_bytes());
        }
    }
    out
}

// Doom SIDEDEFS entry: 30 bytes with three 8-byte texture names.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DoomSidedef {
    pub offset_x: i16,
    pub offset_y: i16,
    pub texture_high: String,
    pub texture_low: String,
    pub texture_mid: String,
    pub sector: u16,
}

pub fn read_doom_sidedefs(data: &[u8]) -> Vec<DoomSidedef> {
    data.chunks_exact(30)
        .map(|c| DoomSidedef {
            offset_x: i16le(c, 0),
            offset_y: i16le(c, 2),
            texture_high: lump_name::make_normal_name(&c[4..12]),
            texture_low: lump_name::make_normal_name(&c[12..20]),
            texture_mid: lump_name::make_normal_name(&c[20..28]),
            sector: u16le(c, 28),
        })
        .collect()
}

pub fn write_doom_sidedefs(sidedefs: &[DoomSidedef]) -> Vec<u8> {
    let mut out = Vec::with_capacity(sidedefs.len() * 30);
    for s in sidedefs {
        out.extend_from_slice(&s.offset_x.to_le_bytes());
        out.extend_from_slice(&s.offset_y.to_le_bytes());
        for tex in [&s.texture_high, &s.texture_low, &s.texture_mid] {
            out.extend_from_slice(&lump_name::make_fixed_name(tex)[..8]);
        }
        out.extend_from_slice(&s.sector.to_le_bytes());
    }
    out
}

// Doom SECTORS entry: 26 bytes with two 8-byte flat names.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DoomSector {
    pub height_floor: i16,
    pub height_ceiling: i16,
    pub texture_floor: String,
    pub texture_ceiling: String,
    pub brightness: i16,
    pub special: u16,
    pub tag: u16,
}

pub fn read_doom_sectors(data: &[u8]) -> Vec<DoomSector> {
    data.chunks_exact(26)
        .map(|c| DoomSector {
            height_floor: i16le(c, 0),
            height_ceiling: i16le(c, 2),
            texture_floor: lump_name::make_normal_name(&c[4..12]),
            texture_ceiling: lump_name::make_normal_name(&c[12..20]),
            brightness: i16le(c, 20),
            special: u16le(c, 22),
            tag: u16le(c, 24),
        })
        .collect()
}

pub fn write_doom_sectors(sectors: &[DoomSector]) -> Vec<u8> {
    let mut out = Vec::with_capacity(sectors.len() * 26);
    for s in sectors {
        out.extend_from_slice(&s.height_floor.to_le_bytes());
        out.extend_from_slice(&s.height_ceiling.to_le_bytes());
        out.extend_from_slice(&lump_name::make_fixed_name(&s.texture_floor)[..8]);
        out.extend_from_slice(&lump_name::make_fixed_name(&s.texture_ceiling)[..8]);
        out.extend_from_slice(&s.brightness.to_le_bytes());
        out.extend_from_slice(&s.special.to_le_bytes());
        out.extend_from_slice(&s.tag.to_le_bytes());
    }
    out
}

#[cfg(test)]
mod doom_codec_tests {
    use super::*;

    #[test]
    fn things_round_trip_with_unsigned_flags() {
        let t = vec![DoomThing {
            x: -128,
            y: 256,
            angle: 90,
            thing_type: 3001,
            flags: 0x8007, // above signed-short range, preserved unsigned
        }];
        assert_eq!(t, read_doom_things(&write_doom_things(&t)));
    }

    #[test]
    fn linedefs_round_trip_with_missing_back_sidedef() {
        let l = vec![DoomLinedef {
            v1: 0,
            v2: 1,
            flags: 1,
            action: 0,
            tag: 0,
            front: 0,
            back: NO_SIDEDEF,
        }];
        let back = read_doom_linedefs(&write_doom_linedefs(&l));
        assert_eq!(l, back);
        assert_eq!(NO_SIDEDEF, back[0].back);
    }

    #[test]
    fn sidedefs_round_trip_with_texture_names() {
        let s = vec![DoomSidedef {
            offset_x: -8,
            offset_y: 16,
            texture_high: "-".into(),
            texture_low: "BROWN1".into(),
            texture_mid: "STARTAN2".into(),
            sector: 5,
        }];
        assert_eq!(s, read_doom_sidedefs(&write_doom_sidedefs(&s)));
    }

    #[test]
    fn sectors_round_trip_with_flat_names() {
        let s = vec![DoomSector {
            height_floor: -16,
            height_ceiling: 128,
            texture_floor: "FLOOR4_8".into(),
            texture_ceiling: "CEIL3_5".into(),
            brightness: 192,
            special: 9,
            tag: 667,
        }];
        assert_eq!(s, read_doom_sectors(&write_doom_sectors(&s)));
    }
}
