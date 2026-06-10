// ABOUTME: WAD resource reading; first slice of UDB WADReader porting.
// ABOUTME: Marker-range namespaces, in-range lookup, last-lump text resources, PLAYPAL.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::wad::Wad;

/// Inclusive-exclusive lump index ranges between start/end markers, in WAD order.
/// Both classic (F_START) and extended (FF_START) markers open a range like UDB.
pub fn find_ranges(wad: &Wad, starts: &[&str], ends: &[&str]) -> Vec<(usize, usize)> {
    let mut ranges = Vec::new();
    let mut open: Option<usize> = None;
    for (i, lump) in wad.lumps().iter().enumerate() {
        let name = lump.name();
        if starts.contains(&name) {
            open = Some(i + 1);
        } else if ends.contains(&name) {
            if let Some(s) = open.take() {
                ranges.push((s, i));
            }
        }
    }
    ranges
}

pub fn flat_ranges(wad: &Wad) -> Vec<(usize, usize)> {
    find_ranges(wad, &["F_START", "FF_START"], &["F_END", "FF_END"])
}

pub fn patch_ranges(wad: &Wad) -> Vec<(usize, usize)> {
    find_ranges(wad, &["P_START", "PP_START"], &["P_END", "PP_END"])
}

pub fn sprite_ranges(wad: &Wad) -> Vec<(usize, usize)> {
    find_ranges(wad, &["S_START", "SS_START"], &["S_END", "SS_END"])
}

/// Find a lump inside the given ranges; later matches win like UDB's
/// last-resource-priority lookups.
pub fn find_in_ranges(wad: &Wad, ranges: &[(usize, usize)], name: &str) -> Option<usize> {
    let mut found = None;
    for &(start, end) in ranges {
        for i in start..end {
            if wad.lumps()[i].name() == name {
                found = Some(i);
            }
        }
    }
    found
}

/// Resolve a singular text resource from the last matching lump like UDB
/// (e.g. only the last MAPINFO in a WAD counts).
pub fn find_last_text_lump(wad: &Wad, name: &str) -> Option<usize> {
    wad.find_last_lump_index(name)
}

/// Parse a PLAYPAL lump into 256 RGB triples (first palette only, like the editor).
pub fn read_playpal(data: &[u8]) -> Option<[(u8, u8, u8); 256]> {
    if data.len() < 768 {
        return None;
    }
    let mut pal = [(0u8, 0u8, 0u8); 256];
    for (i, p) in pal.iter_mut().enumerate() {
        *p = (data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
    }
    Some(pal)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn wad_with(names: &[&str]) -> Wad {
        let mut wad = Wad::create();
        for (i, n) in names.iter().enumerate() {
            wad.insert(n, i, 0);
        }
        wad
    }

    #[test]
    fn marker_ranges_open_and_close() {
        let wad = wad_with(&[
            "MAP01", "F_START", "FLAT1", "FLAT2", "F_END", "FF_START", "FLAT3", "FF_END",
        ]);
        assert_eq!(vec![(2, 4), (6, 7)], flat_ranges(&wad));
    }

    #[test]
    fn in_range_lookup_prefers_later_ranges() {
        let mut wad = wad_with(&["F_START", "FLAT1", "F_END", "FF_START", "FLAT1", "FF_END"]);
        let f1 = wad.insert("X", 6, 0); // unrelated
        let _ = f1;
        let ranges = flat_ranges(&wad);
        assert_eq!(Some(4), find_in_ranges(&wad, &ranges, "FLAT1"));
        assert_eq!(None, find_in_ranges(&wad, &ranges, "MISSING"));
    }

    #[test]
    fn lumps_outside_ranges_are_not_found() {
        let wad = wad_with(&["FLAT1", "F_START", "OTHER", "F_END"]);
        assert_eq!(None, find_in_ranges(&wad, &flat_ranges(&wad), "FLAT1"));
    }

    #[test]
    fn last_text_lump_wins() {
        let wad = wad_with(&["MAPINFO", "THINGS", "MAPINFO"]);
        assert_eq!(Some(2), find_last_text_lump(&wad, "MAPINFO"));
    }

    #[test]
    fn playpal_parses_256_rgb_triples() {
        let mut data = vec![0u8; 768];
        data[0] = 255;
        data[765] = 1;
        data[766] = 2;
        data[767] = 3;
        let pal = read_playpal(&data).unwrap();
        assert_eq!((255, 0, 0), pal[0]);
        assert_eq!((1, 2, 3), pal[255]);
        assert!(read_playpal(&data[..767]).is_none());
    }
}
