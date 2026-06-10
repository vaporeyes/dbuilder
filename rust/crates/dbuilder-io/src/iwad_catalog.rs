// ABOUTME: Official IWAD SHA1 catalog generated from DBuilder WAD.cs (UDB WAD.cs).
// ABOUTME: Used to mark official IWADs read-only; source https://github.com/Doom-Utils/iwad-patches

use crate::sha1;

pub const IWAD_HASHES: &[&str] = &[
    "df0040ccb29cc1622e74ceb3b7793a2304cca2c8", // Doom 1.1
    "b5f86a559642a2b3bdfb8a75e91c8da97f057fe6", // Doom 1.2
    "2e89b86859acd9fc1e552f587b710751efcffa8e", // Doom 1.666
    "2c8212631b37f21ad06d18b5638c733a75e179ff", // Doom 1.8
    "7742089b4468a736cadb659a7deca3320fe6dcbd", // Doom 1.9
    "e5ec79505530e151ff0e6f517f3ce1fd65969c46", // Doom Bfg
    "a89b39d91122882214c3088b8cd6b308713bd7c2", // Doom Ppc
    "117015379c529573510be08cf59810aa10bb934e", // Doom Psn
    "f770111ca9eb6d49aead51fcbd398719b462e64b", // Doom Unity 1.0
    "08ab2507e1d525c4c06b6df4f6d5862568a6b009", // Doom Unity 1.1
    "2a8a1ce0f29497a2781b2902c76115fd60d8bbf8", // Doom Unity 1.3
    "9b07b02ab3c275a6a7570c3f73cc20d63a0e3833", // Doom Ud
    "37de4510216eb3ce9a835dd939109443375d10c5", // Doom Xbla
    "1d1d4f69fe14fa255228d8243470678b1b4efdc5", // Doom Xbox
    "997bae5e5a190c5bb3b1fb9e7e3e75b2da88cb27", // Doom 2024 Re-Re-Release 1.0
    "87651324502044f9a6eed403e48853aa16c93e49", // Doom 2024 Re-Re-Release 1.1
    "a4ce5128d57cb129fdd1441c12b58245be55c8ce", // Doom2 1.666g
    "6d559b7ceece4f5ad457415049711992370d520a", // Doom2 1.666
    "70192b8d5aba65c7e633a7c7bcfe7e3e90640c97", // Doom2 1.7a
    "78009057420b792eacff482021db6fe13b370dcc", // Doom2 1.7
    "79c283b18e61b9a989cfd3e0f19a42ea98fda551", // Doom2 1.8
    "d510c877031bbd5f3d198581a2c8651e09b9861f", // Doom2 1.8f
    "7ec7652fcfce8ddc6e801839291f0e28ef1d5ae7", // Doom2 1.9
    "a59548125f59f6aa1a41c22f615557d3dd2e85a9", // Doom2 Bfg
    "f1b6ba94352d53f646b67c01d2da88c5c40e3179", // Doom2 Psn Eur
    "ca8db908a7c9fbac764f34c148f0bcc78d18553e", // Doom2 Psn Usa
    "9b39107b5bcfd1f989bcfe46f68dbc1f49222922", // Doom2 Unity 1.0
    "b723882122e90b61a1d92a11dcfcf9cbf95a407e", // Doom2 Unity 1.1
    "9574851209c9dfbede56db0dee0660ecd51e6150", // Doom2 Unity 1.3
    "55e445badd63d8841ebea887910c26c62c7f525e", // Doom2 Xbla
    "1c91d86cd8a2f3817227986503a6672a5e1613f0", // Doom2 Xbox
    "b7ba1c68631023ea1aab1d7b9f7f6e9afc508f39", // Doom2 Xbox360bfg
    "2cda310805397ae44059bbcaed3cd602f4864a82", // Doom2 Zodiac
    "c745f04a6abc2e6d2a2d52382f45500dd2a260be", // Doom 2 2024 Re-Re-Release 1.0
    "2921cf667359fd3a80aba3c0cf62ab39297e7e9e", // Doom 2 2024 Re-Re-Release 1.1
    "90361e2a538d2388506657252ae41aceeb1ba360", // Plutonia 1.9
    "f131cbe1946d7fddb3caec4aa258c83399c21e60", // Plutonia Anthology
    "85c3517434135a5886111b324955f9288c01046c", // Plutonia Psn Eur
    "327f8c41ebd4138354e9fca63cebbbd1b9489749", // Plutonia Psn Usa
    "54e27b5791fbc5677bf7e83c1de3a92ea3ef935b", // Plutonia Unity 1.0
    "20fd23ee410c466b263a741bbd53bbef573ab47d", // Plutonia Unity 1.3
    "816c7c6b0098f66c299c9253f62bd908456efb63", // Plutonia 2024 Re-Re-Release
    "9fbc66aedef7fe3bae0986cdb9323d2b8db4c9d3", // Tnt 1.9
    "4a65c8b960225505187c36040b41a40b152f8f3e", // Tnt Anthology
    "5066833da047117241cdda05a708b009eb266c91", // Tnt Psn Eur
    "139e26d801a64b404b8d898defca10227a61867b", // Tnt Psn Usa
    "503271390606ebded04a2cfaa1a4e249c0313a9d", // Tnt Unity 1.0
    "ca0f0495a6c2813b49620202774c56560d6d7621", // Tnt Unity 1.3
    "9820e2a3035f0cdd87f69a7d57c59a7a267c9409", // Tnt 2024 Re-Re-Release
    "b5a6cc79cde48d97905b44282e82c4c966a23a87", // Heretic 1.0
    "a54c5d30629976a649119c5ce8babae2ddfb1a60", // Heretic 1.2
    "f489d479371df32f6d280a0cb23b59a35ba2b833", // Heretic 1.3
    "ae797f5fdce845be24a7a24dd5bfc3e762a17bbe", // Hexen Beta
    "ac129c4331bf26f0f080c4a56aaa40d64969c98a", // Hexen 1.0
    "4b53832f0733c1e29e5f1de2428e5475e891af29", // Hexen 1.1
    "4343fbe5aef905ef6d077a1517a50c919e5cc906", // Hexen Mac
    "eb0f3e157b35c34d5a598701f775e789ec85b4ae", // Strife1 1.1
    "64c13b951a845ca7f8081f68138a6181557458d1", // Strife1 1.2
];

//mxd. Checks a WAD image against the official IWAD SHA1 catalog (mirrors WAD.CheckHash).
pub fn is_official_iwad(image: &[u8]) -> bool {
    image.len() > 4
        && &image[0..4] == b"IWAD"
        && IWAD_HASHES.contains(&sha1::hex_digest(image).as_str())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn synthetic_wads_are_not_official() {
        assert!(!is_official_iwad(b"PWAD\x00\x00\x00\x00\x0c\x00\x00\x00"));
        assert!(!is_official_iwad(b"IWAD\x00\x00\x00\x00\x0c\x00\x00\x00"));
        assert!(!is_official_iwad(b""));
    }

    #[test]
    fn catalog_matches_udb_count_and_shape() {
        assert_eq!(58, IWAD_HASHES.len());
        assert!(IWAD_HASHES.iter().all(|h| h.len() == 40));
        assert!(IWAD_HASHES.contains(&"7ec7652fcfce8ddc6e801839291f0e28ef1d5ae7"));
        // Doom2 1.9
    }
}
