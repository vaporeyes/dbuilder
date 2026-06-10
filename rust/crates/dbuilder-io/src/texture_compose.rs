// ABOUTME: Classic wall texture composition; first slice of UDB TEXTUREx porting.
// ABOUTME: Parses PNAMES and TEXTURE1/2 and blits patch pictures onto texture canvases.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::image_decode::RgbaImage;
use crate::lump_name;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct TexturePatch {
    pub origin_x: i32,
    pub origin_y: i32,
    pub pnames_index: usize,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct TextureDef {
    pub name: String,
    pub width: usize,
    pub height: usize,
    pub patches: Vec<TexturePatch>,
}

/// Parse PNAMES: u32 count followed by 8-byte patch names.
pub fn parse_pnames(data: &[u8]) -> Option<Vec<String>> {
    if data.len() < 4 {
        return None;
    }
    let count = u32::from_le_bytes([data[0], data[1], data[2], data[3]]) as usize;
    if data.len() < 4 + count * 8 {
        return None;
    }
    Some(
        (0..count)
            .map(|i| lump_name::make_normal_name(&data[4 + i * 8..4 + i * 8 + 8]))
            .collect(),
    )
}

/// Parse a TEXTURE1/TEXTURE2 lump into texture definitions.
pub fn parse_texturex(data: &[u8]) -> Option<Vec<TextureDef>> {
    let u32at = |p: usize| -> Option<u32> {
        data.get(p..p + 4)
            .map(|b| u32::from_le_bytes([b[0], b[1], b[2], b[3]]))
    };
    let i16at =
        |p: usize| -> Option<i16> { data.get(p..p + 2).map(|b| i16::from_le_bytes([b[0], b[1]])) };

    let count = u32at(0)? as usize;
    let mut out = Vec::with_capacity(count);
    for i in 0..count {
        let off = u32at(4 + i * 4)? as usize;
        let name = lump_name::make_normal_name(data.get(off..off + 8)?);
        let width = i16at(off + 12)? as i32;
        let height = i16at(off + 14)? as i32;
        if width <= 0 || height <= 0 {
            return None;
        }
        let patch_count = i16at(off + 20)? as usize;
        let mut patches = Vec::with_capacity(patch_count);
        for p in 0..patch_count {
            let pp = off + 22 + p * 10;
            patches.push(TexturePatch {
                origin_x: i16at(pp)? as i32,
                origin_y: i16at(pp + 2)? as i32,
                pnames_index: i16at(pp + 4)? as u16 as usize,
            });
        }
        out.push(TextureDef {
            name,
            width: width as usize,
            height: height as usize,
            patches,
        });
    }
    Some(out)
}

/// Compose a texture by blitting its patches in order. Patches resolve through
/// PNAMES and the caller-supplied picture lookup; missing patches are skipped
/// (UDB warns and continues). Opaque patch pixels overwrite, transparent keep.
pub fn compose(
    def: &TextureDef,
    pnames: &[String],
    lookup: &dyn Fn(&str) -> Option<RgbaImage>,
) -> RgbaImage {
    let mut pixels = vec![0u8; def.width * def.height * 4];
    for patch in &def.patches {
        let Some(name) = pnames.get(patch.pnames_index) else {
            continue;
        };
        let Some(img) = lookup(name) else { continue };
        for sy in 0..img.height {
            let ty = patch.origin_y + sy as i32;
            if ty < 0 || ty as usize >= def.height {
                continue;
            }
            for sx in 0..img.width {
                let tx = patch.origin_x + sx as i32;
                if tx < 0 || tx as usize >= def.width {
                    continue;
                }
                let s = (sy * img.width + sx) * 4;
                if img.pixels[s + 3] != 0 {
                    let t = (ty as usize * def.width + tx as usize) * 4;
                    pixels[t..t + 4].copy_from_slice(&img.pixels[s..s + 4]);
                }
            }
        }
    }
    RgbaImage {
        width: def.width,
        height: def.height,
        offset_x: 0,
        offset_y: 0,
        pixels,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn red_pixel_image() -> RgbaImage {
        RgbaImage {
            width: 1,
            height: 1,
            offset_x: 0,
            offset_y: 0,
            pixels: vec![255, 0, 0, 255],
        }
    }

    #[test]
    fn pnames_parse_and_reject_short_data() {
        let mut d = Vec::new();
        d.extend_from_slice(&2u32.to_le_bytes());
        d.extend_from_slice(b"WALL00\0\0");
        d.extend_from_slice(b"DOOR2\0\0\0");
        assert_eq!(
            vec!["WALL00".to_string(), "DOOR2".to_string()],
            parse_pnames(&d).unwrap()
        );
        assert!(parse_pnames(&d[..11]).is_none());
    }

    #[test]
    fn texturex_parses_definitions() {
        let mut d = Vec::new();
        d.extend_from_slice(&1u32.to_le_bytes());
        d.extend_from_slice(&8u32.to_le_bytes()); // offset to def
        d.extend_from_slice(b"STARTAN2"); // name
        d.extend_from_slice(&0u32.to_le_bytes()); // masked
        d.extend_from_slice(&2i16.to_le_bytes()); // width
        d.extend_from_slice(&2i16.to_le_bytes()); // height
        d.extend_from_slice(&0u32.to_le_bytes()); // columndirectory
        d.extend_from_slice(&1i16.to_le_bytes()); // patchcount
        d.extend_from_slice(&1i16.to_le_bytes()); // originx
        d.extend_from_slice(&0i16.to_le_bytes()); // originy
        d.extend_from_slice(&0i16.to_le_bytes()); // patch index
        d.extend_from_slice(&0i16.to_le_bytes()); // stepdir
        d.extend_from_slice(&0i16.to_le_bytes()); // colormap
        let defs = parse_texturex(&d).unwrap();
        assert_eq!("STARTAN2", defs[0].name);
        assert_eq!((2, 2), (defs[0].width, defs[0].height));
        assert_eq!(
            TexturePatch {
                origin_x: 1,
                origin_y: 0,
                pnames_index: 0
            },
            defs[0].patches[0]
        );
    }

    #[test]
    fn compose_blits_at_origin_and_skips_missing() {
        let def = TextureDef {
            name: "T".into(),
            width: 2,
            height: 1,
            patches: vec![
                TexturePatch {
                    origin_x: 1,
                    origin_y: 0,
                    pnames_index: 0,
                },
                TexturePatch {
                    origin_x: 0,
                    origin_y: 0,
                    pnames_index: 9,
                }, // out of pnames
            ],
        };
        let pnames = vec!["WALL00".to_string()];
        let img = compose(&def, &pnames, &|n| (n == "WALL00").then(red_pixel_image));
        assert_eq!(0, img.pixels[3]); // (0,0) untouched, transparent
        assert_eq!(&[255, 0, 0, 255], &img.pixels[4..8]); // (1,0) blitted red
    }
}
