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
| `Source/Core/Config` | partial | `DBuilder.IO` | Basic configuration parsing exists, including build defaults, required archives, static limits, argument metadata, enum lists and items, generalized categories, options, and bits, thing categories and type metadata, linedef action metadata, sector effects, texture sets, matching texture filters, resource texture sets, linedef activations, flag translations, compiler lists, nodebuilder lists, and several type sections. Full inheritance, includes, and remaining type sections remain. |
| `Source/Core/Controls` | partial | `DBuilder.Editor` | Avalonia dialogs exist for selected workflows only. |
| `Source/Core/Data` | partial | `DBuilder.IO` | Resource manager exists, but full UDB DataManager parity is incomplete. |
| `Source/Core/Dehacked` | partial | `DBuilder.IO` | Patch data model, parser behavior, sprite replacements, and thing catalog merge support exist. Full UDB baseline DehackedData table processing remains. |
| `Source/Core/Editing` | partial | `DBuilder.Editor`, `DBuilder.Map` | Selected editing workflows exist. Full edit-mode lifecycle remains. |
| `Source/Core/General` | partial | `DBuilder.Editor`, `DBuilder.IO` | Settings and launch helpers exist. Full application orchestration remains. |
| `Source/Core/Geometry` | partial | `DBuilder.Geometry`, `DBuilder.Map` | Many helpers are ported. Remaining `Tools.cs` and edge behavior need comparison. |
| `Source/Core/GZBuilder` | partial | `DBuilder.Editor` | Main editor shell exists. Full shell behavior remains. |
| `Source/Core/IO` | partial | `DBuilder.IO` | WAD and map IO exist, including static format constraints before saves. Full stream, conversion, and save semantics remain. |
| `Source/Core/Map` | partial | `DBuilder.Map`, `DBuilder.IO` | Core map elements exist. Map options, grid setup, selection groups, disposal and orphan cleanup, typed field and argument helpers, tag collections, element lookup helpers, sidedef part semantics, and static format constraints are ported. Broader map behavior remains partial. |
| `Source/Core/Plugins` | missing | none | Plugin framework not ported. |
| `Source/Core/Properties` | partial | `DBuilder.Editor` | Basic property dialogs exist. Full typed property system remains. |
| `Source/Core/Rendering` | partial | `DBuilder.Rendering`, `DBuilder.Editor` | Silk.NET renderer exists. Full render pipeline and visual modes remain. |
| `Source/Core/Resources` | partial | `DBuilder.IO` | WAD, PK3, nested PK3, directory, mixed stack, texture, flat, and sprite resource behavior has focused coverage. Full DataManager, lazy loading, cache invalidation, hires, model, voxel, and advanced namespace behavior remains. |
| `Source/Core/Types` | partial | `DBuilder.IO` | Generalized types and game config subsets exist. Full type manager remains. |
| `Source/Core/VisualModes` | partial | `DBuilder.Editor`, `DBuilder.Map` | Picking and 3D helpers exist. Full visual-mode framework remains. |
| `Source/Core/Windows` | partial | `DBuilder.Editor` | Selected windows exist. Full UDB dialog set remains. |
| `Source/Core/ZDoom` | partial | `DBuilder.IO` | Several parsers exist, including ANIMDEFS, DECORATE/ZScript actor metadata, MODELDEF, SNDINFO, SNDSEQ, TERRAIN, LOCKDEFS, DECALDEF, VOXELDEF, IWADINFO, CVARINFO, REVERBS, X11 RGB, TEXTURES metadata, and MAPINFO numbered actor discovery. Full actor semantics, model rendering, voxel rendering, and Dehacked merge behavior remain. |
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
