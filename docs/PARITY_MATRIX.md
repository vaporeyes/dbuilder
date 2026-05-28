# UDB Parity Matrix

Status values:

- `ported`: DBuilder has an intentional replacement with meaningful test coverage.
- `partial`: DBuilder has a replacement, but known UDB behavior remains missing.
- `missing`: No meaningful DBuilder replacement exists yet.

## Core

| UDB area | Status | DBuilder location | Notes |
| --- | --- | --- | --- |
| `Source/Core/Actions` | missing | `DBuilder.Editor` | Command/action manager not ported. |
| `Source/Core/Compilers` | partial | `DBuilder.IO` | Nodebuilder execution and compiler/nodebuilder metadata parsing exist. Full discovery, script compiler execution, error parsing, and UI integration remain. |
| `Source/Core/Config` | partial | `DBuilder.IO` | Basic configuration parsing exists, including config includes and recursive structure merges, build defaults, editor behavior metadata, map format flags, UDMF feature toggles, compatibility metadata, render style and flag metadata, thing flag comparison metadata, universal field metadata, things filter metadata, brightness levels, damage type/internal sound/ignored file metadata, make-door defaults, default thing flags, test launch metadata, map bounds, sky flat metadata, light level mode, long texture name support, required archives, static limits, script configuration, argument metadata, universal type registry metadata, primitive universal value handlers, random integer and decimal handlers, string universal value handler, enum option, bits, and strings universal value handlers, texture and flat universal value handlers, color universal value handler, angle universal value handlers, thing type and class universal value handlers, thing tag and radius universal value handlers, linedef type and tag universal value handlers, sector effect and tag universal value handlers, polyobject number universal value handler, enum lists and items, generalized categories, options, and bits, thing categories and type metadata, linedef action metadata, sector effects, texture sets, matching texture filters, resource texture sets, linedef activations, flag translations, compiler lists, nodebuilder lists, map lump script-build metadata, texture defaults, default sky textures, texture and flat mixing metadata, and several type sections. Remaining type sections and broader bundled-config parity remain. |
| `Source/Core/Controls` | partial | `DBuilder.Editor` | Avalonia dialogs exist for selected workflows only. Argument property editors now use universal handler metadata for boolean and enum-backed choices. |
| `Source/Core/Data` | partial | `DBuilder.IO` | Resource manager and data-location metadata exist, including resource-list serialization, duplicate combination, display names, PK3 root texture and flat options, WAD strict patch option handling, config-driven texture and flat namespace mixing, palette lookup, main colormap lookup, named colormap lookup, colormap texture-name enumeration, sprite name enumeration, root PK3 classic texture composition with nested-WAD PNAMES fallback, and cache invalidation on clear/dispose. Full UDB DataManager parity is incomplete. |
| `Source/Core/Dehacked` | partial | `DBuilder.IO` | Patch data model, parser behavior, sprite replacements, and thing catalog merge support exist. Full UDB baseline DehackedData table processing remains. |
| `Source/Core/Editing` | partial | `DBuilder.Editor`, `DBuilder.Map` | Selected editing workflows exist. Full edit-mode lifecycle remains. |
| `Source/Core/General` | partial | `DBuilder.Editor`, `DBuilder.IO` | Settings and launch helpers exist. Full application orchestration remains. |
| `Source/Core/Geometry` | partial | `DBuilder.Geometry`, `DBuilder.Map` | Many helpers are ported. Remaining `Tools.cs` and edge behavior need comparison. |
| `Source/Core/GZBuilder` | partial | `DBuilder.Editor` | Main editor shell exists. Full shell behavior remains. |
| `Source/Core/IO` | partial | `DBuilder.IO` | WAD and map IO exist, including static format constraints before saves, config-aware map discovery with map-name-format filtering, collision-safe map sub-lump reads, bounded configured-map-lump find/remove helpers, typed map-lump copying, stream-level UDMF reader and writer facades, UDB-order UDMF element emission, unknown top-level UDMF collection preservation, invalid and zero-length Doom/Hexen/clipboard linedef filtering, Doom-to-Hexen thing flag translation through UDMF, Doom-format conversion argument clearing, Hexen hardcoded line-id action conversion to and from UDMF, UDMF-only element cleanup during binary conversion, configured map-lump ordering during save, configured script and blind-copy lump preservation during save, referenced Doom/Hexen sidedef materialization, invalid and near-zero UDMF linedef filtering, invalid UDMF sidedef filtering, referenced UDMF and clipboard sidedef materialization, UDMF `moreids` normalization, collision-safe map marker renaming, required nodebuilder lump checks, nodebuilder temporary-map cleanup, required config lump creation, duplicate map replacement cleanup, and save-back protection for non-map lumps with map-like names. Full stream, conversion, and save semantics remain. |
| `Source/Core/Map` | partial | `DBuilder.Map`, `DBuilder.IO` | Core map elements exist. Map options, grid setup, selection groups, disposal and orphan cleanup, typed field and argument helpers, tag collections, element lookup helpers, sidedef part semantics, and static format constraints are ported. Broader map behavior remains partial. |
| `Source/Core/Plugins` | missing | none | Plugin framework not ported. |
| `Source/Core/Properties` | partial | `DBuilder.Editor` | Basic property dialogs exist. Full typed property system remains. |
| `Source/Core/Rendering` | partial | `DBuilder.Rendering`, `DBuilder.Editor` | Silk.NET renderer exists. Full render pipeline and visual modes remain. |
| `Source/Core/Resources` | partial | `DBuilder.IO` | WAD, PK3, nested PK3, directory, mixed stack, texture, flat, and sprite resource behavior has focused coverage. Full DataManager, lazy loading, cache invalidation, hires, model, voxel, and advanced namespace behavior remains. |
| `Source/Core/Types` | partial | `DBuilder.IO` | Generalized types and game config subsets exist. Full type manager remains. |
| `Source/Core/VisualModes` | partial | `DBuilder.Editor`, `DBuilder.Map` | Picking and 3D helpers exist. Full visual-mode framework remains. |
| `Source/Core/Windows` | partial | `DBuilder.Editor` | Selected windows exist. Full UDB dialog set remains. |
| `Source/Core/ZDoom` | partial | `DBuilder.IO` | Several parsers exist, including ANIMDEFS, DECORATE/ZScript actor metadata, MODELDEF, SNDINFO, SNDSEQ, TERRAIN, LOCKDEFS, DECALDEF, VOXELDEF, IWADINFO, CVARINFO, REVERBS, X11 RGB, TEXTURES metadata, GLDEFS wall and flat glow metadata, and MAPINFO numbered actor discovery. Full actor semantics, model rendering, voxel rendering, and Dehacked merge behavior remain. |
| `Source/Native/OpenGL` | partial | `DBuilder.Rendering` | Replaced with Silk.NET abstraction. Full parity not established. |

## Bundled Plugins

| UDB plugin | Status | DBuilder location | Notes |
| --- | --- | --- | --- |
| `3DFloorMode` | partial | `DBuilder.Map` | 3D floor data helper exists. Full plugin workflow missing. |
| `AutomapMode` | missing | none | Not ported. |
| `BlockmapExplorer` | partial | `DBuilder.Map` | Blockmap logic exists. Explorer UI missing. |
| `BuilderEffects` | partial | `DBuilder.Map` | Selected effects exist. Full plugin missing. |
| `BuilderModes` | partial | `DBuilder.Editor`, `DBuilder.Map` | Selected drawing/editing tools exist. Full mode set missing. |
| `ColorPicker` | missing | none | Not ported. |
| `CommentsPanel` | missing | none | Not ported. |
| `ImageDrawingExample` | missing | none | Not ported. |
| `NodesViewer` | partial | `DBuilder.IO` | Nodes reader exists. Viewer UI missing. |
| `RejectExplorer` | partial | `DBuilder.IO` | Reject table logic exists. Explorer UI missing. |
| `SoundPropagationMode` | partial | `DBuilder.Map` | Sound propagation helper exists. Full mode UI missing. |
| `StairSectorBuilder` | partial | `DBuilder.Map`, `DBuilder.Editor` | Stair builder helper and dialog exist. Full plugin parity unknown. |
| `TagExplorer` | partial | `DBuilder.Editor` | Tag list window exists. Full explorer behavior missing. |
| `TagRange` | missing | none | Not ported. |
| `UDBScript` | missing | none | Script API not ported. |
| `USDF` | missing | none | Not ported. |
| `VisplaneExplorer` | missing | none | Not ported. |
| `WadAuthorMode` | missing | none | Not ported. |
