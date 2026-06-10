// ABOUTME: MurmurHash2 32-bit ported from DBuilder MurmurHash2.cs (UDB MurmurHash2.cs).
// ABOUTME: Used by lump long-name hashing; outputs are pinned and must never change.

/*
 * Original Code: HashTableHashing.MurmurHash2 by Davy Landman (MPL 1.1 / GPL 2.0 / LGPL 2.1).
 * UDB integration: Copyright (c) 2007 Pascal vd Heiden, GPL v2 or later.
 */

const M: u32 = 0x5bd1e995;
const R: u32 = 24;

// Hashes the ASCII bytes of the string with UDB's fixed seed. Non-ASCII characters
// encode as '?' like .NET's ASCII encoding.
pub fn hash_str(data: &str) -> u32 {
    let bytes: Vec<u8> = data
        .chars()
        .map(|c| if c.is_ascii() { c as u8 } else { b'?' })
        .collect();
    hash(&bytes, 0xc58f1a7b)
}

fn hash(data: &[u8], seed: u32) -> u32 {
    let length = data.len();
    if length == 0 {
        return 0;
    }

    let mut h = seed ^ length as u32;
    let remaining_bytes = length & 3; // mod 4
    let number_of_loops = length >> 2; // div 4

    let mut i = 0;
    for _ in 0..number_of_loops {
        let mut k = u32::from_le_bytes([data[i], data[i + 1], data[i + 2], data[i + 3]]);
        k = k.wrapping_mul(M);
        k ^= k >> R;
        k = k.wrapping_mul(M);

        h = h.wrapping_mul(M);
        h ^= k;
        i += 4;
    }

    match remaining_bytes {
        3 => {
            h ^= u16::from_le_bytes([data[i], data[i + 1]]) as u32;
            h ^= (data[i + 2] as u32) << 16;
            h = h.wrapping_mul(M);
        }
        2 => {
            h ^= u16::from_le_bytes([data[i], data[i + 1]]) as u32;
            h = h.wrapping_mul(M);
        }
        1 => {
            h ^= data[i] as u32;
            h = h.wrapping_mul(M);
        }
        _ => {}
    }

    // Final mixes ensure the last few bytes are well-incorporated.
    h ^= h >> 13;
    h = h.wrapping_mul(M);
    h ^= h >> 15;

    h
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn empty_string_hashes_to_zero() {
        assert_eq!(0, hash_str(""));
    }

    #[test]
    fn outputs_match_the_csharp_implementation() {
        // Pinned against DBuilder.IO MurmurHash2.Hash to guarantee cross-language
        // long-name lookup compatibility.
        assert_eq!(0x0b2d6979, hash_str("MAP01"));
        assert_eq!(0x708d7d0e, hash_str("THINGS"));
        assert_eq!(0x22eaa165, hash_str("A"));
        assert_eq!(0x8c257b32, hash_str("AB"));
        assert_eq!(0x0fe9892f, hash_str("ABC"));
        assert_eq!(0x51c24670, hash_str("ABCD"));
        assert_eq!(0xe2628233, hash_str("ABCDEFGH"));
        assert_eq!(0x4c2c20ef, hash_str("SUPERLON"));
        assert_eq!(0x72ce5c64, hash_str("SUPERLONGTEXTURENAME"));
    }

    #[test]
    fn different_inputs_produce_different_hashes() {
        assert_ne!(hash_str("MAP01"), hash_str("MAP02"));
        assert_ne!(hash_str("MAP01"), hash_str("THINGS"));
        assert_ne!(hash_str("A"), hash_str("B"));
    }
}
