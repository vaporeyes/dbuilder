// ABOUTME: WAD lump name encoding helpers ported from DBuilder Lump.cs (UDB Lump.cs).
// ABOUTME: Free functions; the Lump struct itself lands with the WAD archive port.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::murmur_hash2;

// Classic 8-character WAD lump name limit (UDB calls this DataManager.CLASIC_IMAGE_NAME_LENGTH).
pub const CLASSIC_NAME_LENGTH: usize = 8;

// Allowed characters in a map lump name
pub const MAP_LUMP_NAME_CHARS: &str = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_";

//mxd. Stable (hopefully unique) hash value for a texture name of any length.
// The flag controls whether the name is first truncated to the classic 8-char limit.
// Returns i64 like the C# long; the u32 hash is zero-extended.
pub fn make_long_name(name: &str, use_long_names: bool) -> i64 {
    // biwa. ToUpper can produce clashes between names that differ only in case; matches UDB behavior.
    let mut name = name.to_uppercase();
    if !use_long_names && name.chars().count() > CLASSIC_NAME_LENGTH {
        name = name.chars().take(CLASSIC_NAME_LENGTH).collect();
    }
    murmur_hash2::hash_str(&name) as i64
}

// Trim trailing nulls and convert to upper-case ASCII.
pub fn make_normal_name(fixedname: &[u8]) -> String {
    let mut length = 0;
    while length < fixedname.len() && fixedname[length] != 0 {
        length += 1;
    }
    ascii_decode(&fixedname[..length]).trim().to_uppercase()
}

// Encode to the 8-byte (zero-padded) on-disk lump name.
pub fn make_fixed_name(name: &str) -> Vec<u8> {
    let uppername = name.trim().to_uppercase();
    let encoded = ascii_encode(&uppername);
    let mut bytes = encoded.len();
    if bytes < CLASSIC_NAME_LENGTH {
        bytes = CLASSIC_NAME_LENGTH;
    }

    let mut fixedname = vec![0u8; bytes];
    fixedname[..encoded.len()].copy_from_slice(&encoded);
    fixedname
}

// .NET ASCII encoding maps non-ASCII characters to '?'.
fn ascii_encode(s: &str) -> Vec<u8> {
    s.chars()
        .map(|c| if c.is_ascii() { c as u8 } else { b'?' })
        .collect()
}

// .NET ASCII decoding maps bytes above 0x7F to '?'.
fn ascii_decode(bytes: &[u8]) -> String {
    bytes
        .iter()
        .map(|&b| if b < 0x80 { b as char } else { '?' })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn fixed_name_pads_to_8_bytes_and_uppercases() {
        let fn_bytes = make_fixed_name("things");
        assert_eq!(8, fn_bytes.len());
        assert_eq!(b"THINGS", &fn_bytes[..6]);
        assert_eq!(0, fn_bytes[6]);
        assert_eq!(0, fn_bytes[7]);
    }

    #[test]
    fn fixed_name_keeps_long_names_unpadded() {
        let fn_bytes = make_fixed_name("SUPERLONGTEXTURENAME");
        assert_eq!(b"SUPERLONGTEXTURENAME".len(), fn_bytes.len());
    }

    #[test]
    fn normal_name_strips_null_padding() {
        assert_eq!("LINEDEFS", make_normal_name(b"LINEDEFS"));
        assert_eq!("MAP01", make_normal_name(b"MAP01\0\0\0"));
    }

    #[test]
    fn make_long_name_truncates_at_classic_length() {
        let classic = make_long_name("SUPERLONGTEXTURENAME", false);
        let classic_truncated = make_long_name("SUPERLON", false);
        assert_eq!(classic_truncated, classic);
    }

    #[test]
    fn make_long_name_long_variant_does_not_truncate() {
        let long_name = make_long_name("SUPERLONGTEXTURENAME", true);
        let classic = make_long_name("SUPERLONGTEXTURENAME", false);
        assert_ne!(classic, long_name);
    }

    #[test]
    fn make_long_name_matches_csharp_hashes() {
        // Same pinned values as the murmur_hash2 cross-language test, via the
        // case-folding and truncation path.
        assert_eq!(0x0b2d6979, make_long_name("map01", true));
        assert_eq!(0x4c2c20ef, make_long_name("superlongtexturename", false));
    }
}
