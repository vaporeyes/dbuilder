# UDB Parity Matrix

Status values:

- `ported`: DBuilder has an intentional replacement with meaningful test coverage.
- `partial`: DBuilder has a replacement, but known UDB behavior remains missing.
- `missing`: No meaningful DBuilder replacement exists yet.

## Core

| UDB area | Status | DBuilder location | Notes |
| --- | --- | --- | --- |
| `Source/Core/Actions` | missing | `DBuilder.Editor` | Command/action manager not ported. |
| `Source/Core/Compilers` | missing | none | Compiler discovery and execution not ported. |
| `Source/Core/Config` | partial | `DBuilder.IO` | Basic configuration parsing exists. Full inheritance, includes, defaults, and type sections remain. |
| `Source/Core/Controls` | partial | `DBuilder.Editor` | Avalonia dialogs exist for selected workflows only. |
| `Source/Core/Data` | partial | `DBuilder.IO` | Resource manager exists, but full UDB DataManager parity is incomplete. |
| `Source/Core/Dehacked` | missing | none | Dehacked data and parser behavior not ported. |
| `Source/Core/Editing` | partial | `DBuilder.Editor`, `DBuilder.Map` | Selected editing workflows exist. Full edit-mode lifecycle remains. |
| `Source/Core/General` | partial | `DBuilder.Editor`, `DBuilder.IO` | Settings and launch helpers exist. Full application orchestration remains. |
| `Source/Core/Geometry` | partial | `DBuilder.Geometry`, `DBuilder.Map` | Many helpers are ported. Remaining `Tools.cs` and edge behavior need comparison. |
| `Source/Core/GZBuilder` | partial | `DBuilder.Editor` | Main editor shell exists. Full shell behavior remains. |
| `Source/Core/IO` | partial | `DBuilder.IO` | WAD and map IO exist, including static format constraints before saves. Full stream, conversion, and save semantics remain. |
| `Source/Core/Map` | partial | `DBuilder.Map`, `DBuilder.IO` | Core map elements exist. Map options, grid setup, selection groups, typed field and argument helpers, sidedef part semantics, and static format constraints are ported. Remaining collection behavior remains. |
| `Source/Core/Plugins` | missing | none | Plugin framework not ported. |
| `Source/Core/Properties` | partial | `DBuilder.Editor` | Basic property dialogs exist. Full typed property system remains. |
| `Source/Core/Rendering` | partial | `DBuilder.Rendering`, `DBuilder.Editor` | Silk.NET renderer exists. Full render pipeline and visual modes remain. |
| `Source/Core/Resources` | partial | `DBuilder.IO` | Basic texture, flat, sprite, and WAD resource behavior exists. Full priority/cache/PK3 behavior remains. |
| `Source/Core/Types` | partial | `DBuilder.IO` | Generalized types and game config subsets exist. Full type manager remains. |
| `Source/Core/VisualModes` | partial | `DBuilder.Editor`, `DBuilder.Map` | Picking and 3D helpers exist. Full visual-mode framework remains. |
| `Source/Core/Windows` | partial | `DBuilder.Editor` | Selected windows exist. Full UDB dialog set remains. |
| `Source/Core/ZDoom` | partial | `DBuilder.IO` | Several parsers exist. Full actor, metadata, and Dehacked merge behavior remains. |
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
