// ABOUTME: WAD archive ported from DBuilder WAD.cs and Lump.cs (UDB Source/Core/IO).
// ABOUTME: Lumps are offset/length records reading through the owning Wad buffer instead of shared streams.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

use crate::lump_name;

const TYPE_IWAD: &[u8; 4] = b"IWAD";
const TYPE_PWAD: &[u8; 4] = b"PWAD";

#[derive(Debug, PartialEq, Eq)]
pub struct WadError(pub String);

// One directory entry. Data lives in the owning Wad's buffer at [offset, offset + length).
#[derive(Clone, Debug)]
pub struct Lump {
    name: String,
    longname: i64,
    fixedname: Vec<u8>,
    offset: i32,
    length: i32,
}

impl Lump {
    fn new(fixedname: Vec<u8>, offset: i32, length: i32, use_long_names: bool) -> Lump {
        // Make name. Owner's use_long_texture_names determines which longname variant is used.
        let name = lump_name::make_normal_name(&fixedname).to_uppercase();
        let fixedname = lump_name::make_fixed_name(&name);
        let longname = lump_name::make_long_name(&name, use_long_names);
        Lump {
            name,
            longname,
            fixedname,
            offset,
            length,
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn long_name(&self) -> i64 {
        self.longname
    }

    pub fn fixed_name(&self) -> &[u8] {
        &self.fixedname
    }

    pub fn offset(&self) -> i32 {
        self.offset
    }

    pub fn length(&self) -> i32 {
        self.length
    }
}

pub struct Wad {
    buffer: Vec<u8>,
    numlumps: i32,
    lumpsoffset: i32,
    is_iwad: bool,
    is_readonly: bool,
    // Lump-name length policy (UDB read this from General.Map.Config.UseLongTextureNames).
    pub use_long_texture_names: bool,
    is_official_iwad: bool,
    lumps: Vec<Lump>,
}

impl Wad {
    /// Create a new empty in-memory WAD (mirrors opening a stream shorter than 4 bytes).
    pub fn create() -> Wad {
        let mut wad = Wad {
            buffer: Vec::new(),
            numlumps: 0,
            lumpsoffset: 12,
            is_iwad: false,
            is_readonly: false,
            use_long_texture_names: false,
            is_official_iwad: false,
            lumps: Vec::new(),
        };
        wad.write_headers();
        wad
    }

    /// Open a WAD image from bytes (mirrors the Stream constructor + ReadHeaders).
    pub fn from_bytes(buffer: Vec<u8>, readonly: bool) -> Result<Wad, WadError> {
        if buffer.len() < 4 {
            let mut wad = Wad::create();
            wad.is_readonly = readonly;
            return Ok(wad);
        }

        let is_iwad = &buffer[0..4] == TYPE_IWAD;

        let numlumps = read_i32(&buffer, 4)?;
        if numlumps < 0 {
            return Err(WadError("Invalid number of lumps in wad file.".into()));
        }

        let lumpsoffset = read_i32(&buffer, 8)?;
        if lumpsoffset < 0 {
            return Err(WadError("Invalid lumps offset in wad file.".into()));
        }

        let mut lumps = Vec::with_capacity(numlumps as usize);
        let mut pos = lumpsoffset as usize;
        for _ in 0..numlumps {
            let offset = read_i32(&buffer, pos)?;
            let length = read_i32(&buffer, pos + 4)?;
            let fixedname = buffer
                .get(pos + 8..pos + 16)
                .ok_or_else(|| WadError("Unexpected end of lump table.".into()))?
                .to_vec();
            lumps.push(Lump::new(fixedname, offset, length, false));
            pos += 16;
        }

        Ok(Wad {
            buffer,
            numlumps,
            lumpsoffset,
            is_iwad,
            is_readonly: readonly,
            use_long_texture_names: false,
            is_official_iwad: false,
            lumps,
        })
    }

    /// The full on-disk image, including headers and lump table.
    pub fn to_bytes(&self) -> &[u8] {
        &self.buffer
    }

    pub fn is_iwad(&self) -> bool {
        self.is_iwad
    }

    pub fn set_is_iwad(&mut self, value: bool) {
        self.is_iwad = value;
    }

    pub fn is_readonly(&self) -> bool {
        self.is_readonly
    }

    //mxd. True when the image matched the official IWAD SHA1 catalog on open.
    pub fn is_official_iwad(&self) -> bool {
        self.is_official_iwad
    }

    pub fn lumps(&self) -> &[Lump] {
        &self.lumps
    }

    // Writes the WAD header and lumps table
    pub fn write_headers(&mut self) {
        // [ZZ] don't allow any edit actions on readonly archive
        if self.is_readonly {
            return;
        }

        let table_end = self.lumpsoffset as usize + self.lumps.len() * 16;
        if self.buffer.len() < table_end.max(12) {
            self.buffer.resize(table_end.max(12), 0);
        }

        let sig = if self.is_iwad { TYPE_IWAD } else { TYPE_PWAD };
        self.buffer[0..4].copy_from_slice(sig);
        self.buffer[4..8].copy_from_slice(&self.numlumps.to_le_bytes());
        self.buffer[8..12].copy_from_slice(&self.lumpsoffset.to_le_bytes());

        let mut pos = self.lumpsoffset as usize;
        for lump in &self.lumps {
            self.buffer[pos..pos + 4].copy_from_slice(&lump.offset.to_le_bytes());
            self.buffer[pos + 4..pos + 8].copy_from_slice(&lump.length.to_le_bytes());
            let mut name8 = [0u8; 8];
            let n = lump.fixedname.len().min(8);
            name8[..n].copy_from_slice(&lump.fixedname[..n]);
            self.buffer[pos + 8..pos + 16].copy_from_slice(&name8);
            pos += 16;
        }
    }

    // Creates a new lump in the WAD file. Returns the lump index, or None on readonly archives.
    pub fn insert(&mut self, name: &str, position: usize, datalength: i32) -> Option<usize> {
        // [ZZ] don't allow any edit actions on readonly archive
        if self.is_readonly {
            return None;
        }

        self.numlumps += 1;

        self.buffer
            .resize(self.buffer.len() + datalength as usize + 16, 0);

        let fixedname = lump_name::make_fixed_name(name);
        let lump = Lump::new(
            fixedname,
            self.lumpsoffset,
            datalength,
            self.use_long_texture_names,
        );
        self.lumps.insert(position, lump);

        self.lumpsoffset += datalength;

        self.write_headers();

        Some(position)
    }

    pub fn remove_at(&mut self, index: usize) {
        // [ZZ] don't allow any edit actions on readonly archive
        if self.is_readonly {
            return;
        }

        self.lumps.remove(index);
        self.numlumps -= 1;

        self.write_headers();
    }

    /// Lump data bytes (the ClippedStream view in C#).
    pub fn lump_data(&self, index: usize) -> &[u8] {
        let lump = &self.lumps[index];
        &self.buffer[lump.offset as usize..(lump.offset + lump.length) as usize]
    }

    /// Overwrite lump data in place; data must fit the lump's declared length.
    pub fn set_lump_data(&mut self, index: usize, data: &[u8]) {
        if self.is_readonly {
            return;
        }
        let lump = &self.lumps[index];
        assert!(
            data.len() <= lump.length as usize,
            "data exceeds lump length"
        );
        let start = lump.offset as usize;
        self.buffer[start..start + data.len()].copy_from_slice(data);
    }

    pub fn find_lump_index(&self, name: &str) -> Option<usize> {
        self.find_lump_index_between(name, 0, self.lumps.len().wrapping_sub(1))
    }

    pub fn find_lump_index_between(&self, name: &str, start: usize, end: usize) -> Option<usize> {
        //mxd. Can't be here. Go away!
        if name.len() > 8 || self.lumps.is_empty() || start > self.lumps.len() - 1 {
            return None;
        }

        let longname = lump_name::make_long_name(name, self.use_long_texture_names);

        let end = end.min(self.lumps.len() - 1);

        (start..=end).find(|&i| self.lumps[i].longname == longname)
    }

    //mxd. Same as above, but searches in reversed order.
    pub fn find_last_lump_index(&self, name: &str) -> Option<usize> {
        if name.len() > 8 || self.lumps.is_empty() {
            return None;
        }

        let longname = lump_name::make_long_name(name, self.use_long_texture_names);

        (0..self.lumps.len())
            .rev()
            .find(|&i| self.lumps[i].longname == longname)
    }
}

fn read_i32(buffer: &[u8], pos: usize) -> Result<i32, WadError> {
    let bytes = buffer
        .get(pos..pos + 4)
        .ok_or_else(|| WadError("Unexpected end of wad file.".into()))?;
    Ok(i32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn empty_wad_in_memory_has_pwad_header() {
        let wad = Wad::create();
        assert!(!wad.is_iwad());
        assert!(wad.lumps().is_empty());
        assert_eq!(b"PWAD", &wad.to_bytes()[0..4]);
    }

    #[test]
    fn inserted_lumps_round_trip_through_wad() {
        let things_data = [0xDEu8, 0xAD, 0xBE, 0xEF];
        let linedefs_data = [0x01u8, 0x02, 0x03, 0x04, 0x05, 0x06];

        let mut wad = Wad::create();
        let things = wad.insert("THINGS", 0, things_data.len() as i32).unwrap();
        wad.set_lump_data(things, &things_data);
        let linedefs = wad
            .insert("LINEDEFS", 1, linedefs_data.len() as i32)
            .unwrap();
        wad.set_lump_data(linedefs, &linedefs_data);
        wad.write_headers();

        let wad2 = Wad::from_bytes(wad.to_bytes().to_vec(), true).expect("valid wad");
        assert_eq!(2, wad2.lumps().len());
        assert_eq!("THINGS", wad2.lumps()[0].name());
        assert_eq!("LINEDEFS", wad2.lumps()[1].name());
        assert_eq!(&things_data, wad2.lump_data(0));
        assert_eq!(&linedefs_data, wad2.lump_data(1));
    }

    #[test]
    fn find_lump_finds_by_case_insensitive_name() {
        let mut wad = Wad::create();
        wad.insert("MAP01", 0, 0);
        wad.insert("VERTEXES", 1, 0);

        assert!(wad.find_lump_index("map01").is_some());
        assert!(wad.find_lump_index("VERTEXES").is_some());
        assert!(wad.find_lump_index("MAP02").is_none());
    }

    #[test]
    fn find_lump_returns_none_for_names_over_8_chars() {
        // FindLumpIndex shortcuts long names; for the WAD-level API names over 8 chars are invalid.
        let mut wad = Wad::create();
        wad.insert("MAP01", 0, 0);
        assert!(wad.find_lump_index("VERYLONGNAME").is_none());
    }

    #[test]
    fn find_last_lump_index_searches_in_reverse() {
        let mut wad = Wad::create();
        wad.insert("MAP01", 0, 0);
        wad.insert("THINGS", 1, 0);
        wad.insert("MAP01", 2, 0);

        assert_eq!(Some(0), wad.find_lump_index("MAP01"));
        assert_eq!(Some(2), wad.find_last_lump_index("MAP01"));
    }

    #[test]
    fn remove_at_drops_lump() {
        let mut wad = Wad::create();
        wad.insert("A", 0, 4);
        wad.insert("B", 1, 4);
        wad.insert("C", 2, 4);
        assert_eq!(3, wad.lumps().len());

        wad.remove_at(1);
        assert_eq!(2, wad.lumps().len());
        assert_eq!("A", wad.lumps()[0].name());
        assert_eq!("C", wad.lumps()[1].name());
    }

    #[test]
    fn read_only_wad_rejects_mutation() {
        let mut wad = Wad::create();
        wad.insert("X", 0, 0);
        wad.write_headers();

        let mut read_only = Wad::from_bytes(wad.to_bytes().to_vec(), true).expect("valid wad");
        assert!(read_only.is_readonly());
        // Insert returns None on readonly archives ([ZZ]'s behavior).
        assert!(read_only.insert("Y", 0, 0).is_none());
    }

    #[test]
    fn negative_lump_count_is_rejected() {
        let mut bytes = Vec::new();
        bytes.extend_from_slice(b"PWAD");
        bytes.extend_from_slice(&(-1i32).to_le_bytes());
        bytes.extend_from_slice(&12i32.to_le_bytes());
        assert!(Wad::from_bytes(bytes, true).is_err());
    }

    #[test]
    fn iwad_signature_is_detected() {
        let mut wad = Wad::create();
        wad.set_is_iwad(true);
        wad.write_headers();

        let wad2 = Wad::from_bytes(wad.to_bytes().to_vec(), true).expect("valid wad");
        assert!(wad2.is_iwad());
    }
}

// File-backed opening and saving. UDB writes through a FileStream continuously; the Rust
// port edits the in-memory image and persists it explicitly via save().
impl Wad {
    /// Open a WAD file from disk (mirrors FileMode.OpenOrCreate: a missing or short file
    /// becomes a fresh empty PWAD).
    pub fn open_path(path: &std::path::Path, readonly: bool) -> Result<Wad, WadError> {
        let bytes = match std::fs::read(path) {
            Ok(b) => b,
            Err(e) if e.kind() == std::io::ErrorKind::NotFound => Vec::new(),
            Err(e) => return Err(WadError(e.to_string())),
        };
        //mxd. Official IWADs are forced read-only like UDB's CheckHash.
        let official = crate::iwad_catalog::is_official_iwad(&bytes);
        let mut wad = Wad::from_bytes(bytes, readonly || official)?;
        wad.is_official_iwad = official;
        Ok(wad)
    }

    /// Persist the current image to disk. No-op on readonly archives.
    pub fn save_to_path(&self, path: &std::path::Path) -> Result<(), WadError> {
        if self.is_readonly {
            return Ok(());
        }
        std::fs::write(path, &self.buffer).map_err(|e| WadError(e.to_string()))
    }

    //mxd. Rebuilds the WAD file, removing all "dead" entries.
    // Tech info: remove() doesn't remove lump data, so temporary map files slowly grow
    // with every save; this compacts the image like UDB's Compress().
    pub fn compress(&mut self) {
        if self.is_readonly {
            return;
        }

        let mut totaldatalength = 0i32;
        let mut copydata: Vec<(Vec<u8>, Vec<u8>)> = Vec::with_capacity(self.lumps.len());
        for i in 0..self.lumps.len() {
            let data = self.lump_data(i).to_vec();
            copydata.push((self.lumps[i].fixedname.clone(), data));
            totaldatalength += self.lumps[i].length;
        }

        if totaldatalength >= self.lumpsoffset + 12 {
            return;
        }

        self.buffer = vec![0u8; totaldatalength as usize + copydata.len() * 16 + 12];
        self.lumpsoffset = 12;
        self.numlumps = copydata.len() as i32;
        self.lumps = Vec::with_capacity(copydata.len());

        for (fixedname, data) in copydata {
            let lump = Lump::new(
                fixedname,
                self.lumpsoffset,
                data.len() as i32,
                self.use_long_texture_names,
            );
            let start = self.lumpsoffset as usize;
            self.buffer[start..start + data.len()].copy_from_slice(&data);
            self.lumps.push(lump);
            self.lumpsoffset += data.len() as i32;
        }

        self.write_headers();
    }
}

#[cfg(test)]
mod file_tests {
    use super::*;

    #[test]
    fn open_save_round_trips_through_disk() {
        let path = std::env::temp_dir().join("dbuilder_wad_roundtrip_test.wad");
        let _ = std::fs::remove_file(&path);

        let mut wad = Wad::open_path(&path, false).expect("create");
        let i = wad.insert("THINGS", 0, 4).unwrap();
        wad.set_lump_data(i, &[1, 2, 3, 4]);
        wad.write_headers();
        wad.save_to_path(&path).expect("save");

        let wad2 = Wad::open_path(&path, true).expect("reopen");
        assert_eq!(1, wad2.lumps().len());
        assert_eq!("THINGS", wad2.lumps()[0].name());
        assert_eq!(&[1, 2, 3, 4], wad2.lump_data(0));

        let _ = std::fs::remove_file(&path);
    }

    #[test]
    fn compress_reclaims_dead_lump_data() {
        let mut wad = Wad::create();
        let a = wad.insert("A", 0, 64).unwrap();
        wad.set_lump_data(a, &[0xAA; 64]);
        let b = wad.insert("B", 1, 4).unwrap();
        wad.set_lump_data(b, &[1, 2, 3, 4]);
        wad.remove_at(0);
        let before = wad.to_bytes().len();

        wad.compress();

        assert!(wad.to_bytes().len() < before);
        assert_eq!(1, wad.lumps().len());
        assert_eq!("B", wad.lumps()[0].name());
        assert_eq!(&[1, 2, 3, 4], wad.lump_data(0));

        // The compacted image still parses.
        let wad2 = Wad::from_bytes(wad.to_bytes().to_vec(), true).expect("valid wad");
        assert_eq!("B", wad2.lumps()[0].name());
    }
}

impl Wad {
    /// Rename a lump in place and rewrite the directory (mirrors Lump.Rename).
    pub fn rename_lump(&mut self, index: usize, newname: &str) {
        if self.is_readonly {
            return;
        }
        let lump = &mut self.lumps[index];
        lump.fixedname = lump_name::make_fixed_name(newname);
        lump.name = lump_name::make_normal_name(&lump.fixedname).to_uppercase();
        lump.longname = lump_name::make_long_name(newname, self.use_long_texture_names);
        self.write_headers();
    }
}

#[cfg(test)]
mod rename_tests {
    use super::*;

    #[test]
    fn rename_updates_name_and_directory() {
        let mut wad = Wad::create();
        let i = wad.insert("OLDNAME", 0, 4).unwrap();
        wad.set_lump_data(i, &[9, 9, 9, 9]);

        wad.rename_lump(0, "newname");
        assert_eq!("NEWNAME", wad.lumps()[0].name());
        assert!(wad.find_lump_index("NEWNAME").is_some());
        assert!(wad.find_lump_index("OLDNAME").is_none());

        // The rename persists through the on-disk image.
        let wad2 = Wad::from_bytes(wad.to_bytes().to_vec(), true).expect("valid wad");
        assert_eq!("NEWNAME", wad2.lumps()[0].name());
        assert_eq!(&[9, 9, 9, 9], wad2.lump_data(0));
    }
}
