// ABOUTME: Embedded UDMF sample map for the interactive viewer.
// ABOUTME: Two adjacent rooms connected by a corridor plus a handful of things, enough to verify everything renders.

namespace DBuilder.MapViewer;

internal static class SampleMap
{
    // Layout: 512x256 main room on the left, 256x256 room on the right, 64-wide corridor between.
    // 9 verts, 4 sectors, 13 sidedefs, 13 linedefs, 6 things.
    public const string Udmf = """
        namespace = "Doom";

        // Outer left-room vertices (0..3)
        vertex { x = -512; y = -128; }
        vertex { x = 0;    y = -128; }
        vertex { x = 0;    y =  128; }
        vertex { x = -512; y =  128; }
        // Corridor side junction vertices (4..5) - top and bottom of where left room meets corridor
        vertex { x = 0;    y =  -32; }
        vertex { x = 0;    y =   32; }
        // Right room outer (6..9), corridor entries on its left side
        vertex { x = 128;  y =  -32; }
        vertex { x = 128;  y =   32; }
        vertex { x = 384;  y = -128; }
        vertex { x = 384;  y =  128; }
        vertex { x = 128;  y = -128; }
        vertex { x = 128;  y =  128; }

        // Sectors: left-room, corridor, right-room, ceiling decoration
        sector { heightfloor =  0; heightceiling = 192; texturefloor = "FLOOR4_8"; textureceiling = "CEIL3_5"; lightlevel = 192; }
        sector { heightfloor = 16; heightceiling = 128; texturefloor = "STEP1";    textureceiling = "TLITE6_1"; lightlevel = 160; }
        sector { heightfloor =  0; heightceiling = 160; texturefloor = "FLAT5_4";  textureceiling = "CEIL5_1"; lightlevel = 224; }

        // Left-room walls (front-only sidedefs)
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }
        sidedef { sector = 0; texturemiddle = "STARTAN"; }

        // Left-room <-> corridor portal (two-sided sidedefs, indices 4-5)
        sidedef { sector = 0; texturetop = "BROWN1"; texturebottom = "BROWN1"; texturemiddle = "-"; }
        sidedef { sector = 1; texturetop = "BROWN1"; texturebottom = "BROWN1"; texturemiddle = "-"; }

        // Corridor walls (front-only, indices 6-7)
        sidedef { sector = 1; texturemiddle = "BROWN1"; }
        sidedef { sector = 1; texturemiddle = "BROWN1"; }

        // Corridor <-> right-room portal (two-sided sidedefs, indices 8-9)
        sidedef { sector = 1; texturetop = "BROWN1"; texturebottom = "BROWN1"; texturemiddle = "-"; }
        sidedef { sector = 2; texturetop = "BROWN1"; texturebottom = "BROWN1"; texturemiddle = "-"; }

        // Right-room walls (front-only, indices 10-13)
        sidedef { sector = 2; texturemiddle = "BRICK7"; }
        sidedef { sector = 2; texturemiddle = "BRICK7"; }
        sidedef { sector = 2; texturemiddle = "BRICK7"; }
        sidedef { sector = 2; texturemiddle = "BRICK7"; }

        // Left room outline (with the portal segment split into 4->5, between verts 4 and 5)
        linedef { v1 = 0; v2 = 1; sidefront = 0; blocking = true; }
        linedef { v1 = 1; v2 = 4; sidefront = 1; blocking = true; }
        linedef { v1 = 5; v2 = 2; sidefront = 2; blocking = true; }
        linedef { v1 = 2; v2 = 3; sidefront = 3; blocking = true; }
        linedef { v1 = 3; v2 = 0; sidefront = 3; blocking = true; }
        // Portal between left room and corridor
        linedef { v1 = 4; v2 = 5; sidefront = 4; sideback = 5; twosided = true; }

        // Corridor side walls (top and bottom)
        linedef { v1 = 4; v2 = 6; sidefront = 6; blocking = true; }
        linedef { v1 = 7; v2 = 5; sidefront = 7; blocking = true; }
        // Portal between corridor and right room
        linedef { v1 = 6; v2 = 7; sidefront = 8; sideback = 9; twosided = true; }

        // Right room outline
        linedef { v1 = 6; v2 = 10; sidefront = 10; blocking = true; }
        linedef { v1 = 10; v2 = 8; sidefront = 11; blocking = true; }
        linedef { v1 = 8; v2 = 9;  sidefront = 12; blocking = true; }
        linedef { v1 = 9; v2 = 11; sidefront = 12; blocking = true; }
        linedef { v1 = 11; v2 = 7; sidefront = 13; blocking = true; }

        // Things: player start in left room, monsters scattered
        thing { x = -384.0; y =    0.0; angle = 0;   type = 1;    skill1 = true; skill2 = true; skill3 = true; }
        thing { x = -200.0; y =  -64.0; angle = 90;  type = 3004; skill1 = true; skill2 = true; skill3 = true; }
        thing { x = -200.0; y =   64.0; angle = 180; type = 3004; skill1 = true; skill2 = true; skill3 = true; }
        thing { x =  256.0; y =   80.0; angle = 270; type = 3001; skill1 = true; skill2 = true; skill3 = true; }
        thing { x =  300.0; y =  -64.0; angle = 0;   type = 2014; skill1 = true; skill2 = true; skill3 = true; }
        thing { x =  300.0; y =    0.0; angle = 0;   type = 2018; skill1 = true; skill2 = true; skill3 = true; }
        """;
}
