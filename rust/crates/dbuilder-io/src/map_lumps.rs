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
