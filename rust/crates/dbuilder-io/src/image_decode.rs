// ABOUTME: Classic image decoding; first slice of UDB FlatImage/DoomPicture porting.
// ABOUTME: Decodes indexed flats and column/post Doom pictures to RGBA via PLAYPAL.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

pub type Palette = [(u8, u8, u8); 256];

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct RgbaImage {
    pub width: usize,
    pub height: usize,
    pub offset_x: i32,
    pub offset_y: i32,
    /// Row-major RGBA8.
    pub pixels: Vec<u8>,
}

/// Decode a raw indexed flat. Classic flats are 64x64; UDB also accepts other
/// square or known sizes, so the dimensions come from the caller.
pub fn decode_flat(data: &[u8], width: usize, height: usize, pal: &Palette) -> Option<RgbaImage> {
    if data.len() < width * height {
        return None;
    }
    let mut pixels = Vec::with_capacity(width * height * 4);
    for &index in &data[..width * height] {
        let (r, g, b) = pal[index as usize];
        pixels.extend_from_slice(&[r, g, b, 255]);
    }
    Some(RgbaImage {
        width,
        height,
        offset_x: 0,
        offset_y: 0,
        pixels,
    })
}

/// Decode a column/post Doom picture to RGBA. Uncovered pixels stay transparent.
/// Malformed headers, offsets, or posts return None like UDB's reader guards.
pub fn decode_doom_picture(data: &[u8], pal: &Palette) -> Option<RgbaImage> {
    if data.len() < 8 {
        return None;
    }
    let width = i16::from_le_bytes([data[0], data[1]]) as i32;
    let height = i16::from_le_bytes([data[2], data[3]]) as i32;
    let offset_x = i16::from_le_bytes([data[4], data[5]]) as i32;
    let offset_y = i16::from_le_bytes([data[6], data[7]]) as i32;
    if width <= 0 || height <= 0 || width > 4096 || height > 4096 {
        return None;
    }
    let (w, h) = (width as usize, height as usize);
    if data.len() < 8 + w * 4 {
        return None;
    }

    let mut pixels = vec![0u8; w * h * 4];
    for col in 0..w {
        let off_pos = 8 + col * 4;
        let col_off = u32::from_le_bytes([
            data[off_pos],
            data[off_pos + 1],
            data[off_pos + 2],
            data[off_pos + 3],
        ]) as usize;
        let mut p = col_off;
        loop {
            let top_delta = *data.get(p)? as usize;
            if top_delta == 0xFF {
                break;
            }
            let length = *data.get(p + 1)? as usize;
            // Skip the unused padding byte before pixel data.
            p += 3;
            for i in 0..length {
                let index = *data.get(p + i)?;
                let row = top_delta + i;
                if row < h {
                    let at = (row * w + col) * 4;
                    let (r, g, b) = pal[index as usize];
                    pixels[at..at + 4].copy_from_slice(&[r, g, b, 255]);
                }
            }
            // Skip pixels and the trailing padding byte.
            p += length + 1;
        }
    }

    Some(RgbaImage {
        width: w,
        height: h,
        offset_x,
        offset_y,
        pixels,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn pal() -> Palette {
        let mut p = [(0u8, 0u8, 0u8); 256];
        p[1] = (255, 0, 0);
        p[2] = (0, 255, 0);
        p
    }

    #[test]
    fn flat_decodes_indexed_to_rgba() {
        let img = decode_flat(&[1, 2, 1, 2], 2, 2, &pal()).unwrap();
        assert_eq!((2, 2), (img.width, img.height));
        assert_eq!(&[255, 0, 0, 255], &img.pixels[0..4]);
        assert_eq!(&[0, 255, 0, 255], &img.pixels[4..8]);
        assert!(decode_flat(&[1, 2, 1], 2, 2, &pal()).is_none());
    }

    #[test]
    fn doom_picture_decodes_posts_and_transparency() {
        // 2 wide, 2 tall, offsets (1, 3). Column 0: post at row 0, 1 pixel (index 1).
        // Column 1: empty (immediate 0xFF terminator) -> fully transparent.
        let mut d = Vec::new();
        d.extend_from_slice(&2i16.to_le_bytes());
        d.extend_from_slice(&2i16.to_le_bytes());
        d.extend_from_slice(&1i16.to_le_bytes());
        d.extend_from_slice(&3i16.to_le_bytes());
        let col0 = 8 + 8; // after two u32 column offsets
        let col1 = col0 + 5; // topdelta, length, pad, pixel, pad ... then terminator
        d.extend_from_slice(&(col0 as u32).to_le_bytes());
        d.extend_from_slice(&((col1 + 1) as u32).to_le_bytes());
        d.extend_from_slice(&[0, 1, 0, 1, 0, 0xFF]); // col0 post + terminator
        d.push(0xFF); // col1 empty terminator
        let img = decode_doom_picture(&d, &pal()).unwrap();
        assert_eq!(
            (2, 2, 1, 3),
            (img.width, img.height, img.offset_x, img.offset_y)
        );
        assert_eq!(&[255, 0, 0, 255], &img.pixels[0..4]); // (0,0) red
        assert_eq!(0, img.pixels[7]); // (1,0) transparent alpha
        assert_eq!(0, img.pixels[8 + 3]); // (0,1) transparent alpha
    }

    #[test]
    fn malformed_pictures_reject() {
        assert!(decode_doom_picture(&[0, 0], &pal()).is_none());
        let mut d = Vec::new();
        d.extend_from_slice(&1i16.to_le_bytes());
        d.extend_from_slice(&1i16.to_le_bytes());
        d.extend_from_slice(&0i16.to_le_bytes());
        d.extend_from_slice(&0i16.to_le_bytes());
        d.extend_from_slice(&999u32.to_le_bytes()); // column offset past end
        assert!(decode_doom_picture(&d, &pal()).is_none());
    }
}
