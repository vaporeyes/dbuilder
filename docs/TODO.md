# Full Ultimate Doom Builder Port TODO

This document tracks the remaining work to bring DBuilder to feature parity with Ultimate Doom Builder.

## Scope And Assumptions

- Target parity is with `/Users/jsh/dev/repos/UltimateDOOMBuilder`.
- Current DBuilder codebase is C#/.NET/Avalonia, not Rust.
- Full parity means UDB core plus bundled plugins, not only a minimal WAD editor.
- Existing passing tests are treated as baseline behavior to preserve.
- New work should be implemented in small, testable slices and should not refactor unrelated code.

## Current Baseline

- Core geometry, map model, map IO, resource loading, parser coverage, rendering scaffolding, and an Avalonia editor shell exist.
- `DBuilder.Editor` is included in `DBuilder.slnx` and is built by the standard verification script.
- `scripts/verify.sh` is the current baseline gate. It restores, builds the solution, and runs the test suite.
- Current verified baseline: `scripts/verify.sh` passes 877 tests.
- UDB core remains much larger than the current port, and UDB plugins are still mostly unported.

## Solution And Project Structure

- [x] Add `src/DBuilder.Editor/DBuilder.Editor.csproj` to `DBuilder.slnx`.
- [x] Decide whether spike/demo projects remain in the main solution or move to a samples folder.
- [x] Add CI that builds every production project, including the editor.
- [x] Add CI that runs all tests on every supported platform.
- [x] Add a documented project layout that maps DBuilder projects to UDB source areas.
- [x] Add a parity tracker that maps every UDB core folder and plugin to `missing`, `partial`, or `ported`.
- [x] Add test fixtures that can load representative IWAD/PWAD/PK3 resources without committing copyrighted assets.

## Core Application Systems

- [ ] Port UDB `General` application orchestration.
- [ ] Port map manager behavior, including open, close, reload, save, save-as, and dirty-state rules.
- [ ] Port autosave and recovery behavior.
- [ ] Port launcher and external command execution behavior.
- [ ] Port update-check behavior or explicitly replace it with a documented cross-platform equivalent.
- [ ] Port error logging and error display behavior.
- [ ] Port toast, status, and notification behavior.
- [ ] Port recent file and recent map behavior to match UDB.
- [ ] Port file lock checks and save conflict handling.
- [ ] Port program configuration loading and persistence.
- [ ] Port preferences categories and persistence.
- [ ] Port pre-command and post-command support.
- [ ] Port game testing flow with configurable source ports.
- [ ] Port node-builder discovery, configuration, and execution flow.

## Actions, Commands, And Input

- [ ] Port UDB action manager.
- [ ] Port begin/end action attributes or an equivalent command registration system.
- [ ] Port configurable key bindings.
- [ ] Port mouse input abstractions.
- [ ] Port special key handling.
- [ ] Port command hints and contextual help.
- [ ] Port toolbar/menu command synchronization.
- [ ] Port repeatable actions and action state updates.
- [ ] Port command availability rules for selection, mode, and map state.

## Plugin Architecture

- [ ] Design and implement a plugin loading architecture for DBuilder.
- [ ] Define plugin lifecycle hooks equivalent to UDB plugins.
- [ ] Define plugin APIs for map access, UI contribution, actions, edit modes, and dockers.
- [ ] Port plugin toolbar and menu contribution behavior.
- [ ] Port plugin settings persistence.
- [ ] Port plugin resource handling.
- [ ] Port plugin error isolation and reporting.
- [ ] Add compatibility tests for plugin lifecycle behavior.

## Map Model Parity

- [ ] Expand `MapSet` to cover full UDB map behavior.
- [ ] Port all selection, marking, and indexing semantics.
- [x] Port element disposal and orphan cleanup behavior.
- [ ] Port element copy, paste, clone, and serialization behavior.
- [x] Port in-memory selection groups and group metadata.
- [x] Preserve selection groups through clipboard and undo snapshots.
- [x] Port map-options-backed selection group persistence.
- [x] Port remaining map options.
- [ ] Port all UDMF field semantics for vertices, linedefs, sidedefs, sectors, and things.
- [x] Port argument handling and typed field access.
- [x] Port tag behavior and tag collections.
- [x] Port sidedef part semantics.
- [ ] Port sector builder behavior beyond the current subset.
- [ ] Port split-line and merge-geometry behavior to full UDB behavior.
- [ ] Port blockmap behavior and queries to full UDB behavior.
- [x] Port map options and map metadata.
- [x] Port map element collections and lookup behavior.
- [x] Port static limits and map-format constraints.

## Geometry And Analysis

- [ ] Compare every `DBuilder.Geometry` type with the UDB equivalent and close behavior gaps.
- [ ] Port remaining geometry helpers from UDB `Geometry/Tools.cs`.
- [ ] Port full trace path behavior for linedefs and sidedefs.
- [ ] Port curve tools and interpolation behavior fully.
- [ ] Port robust triangulation behavior for all known UDB map shapes.
- [ ] Port projected frustum behavior fully.
- [ ] Port label placement behavior fully.
- [ ] Port sector geometry validation.
- [ ] Port line intersection, snapping, nearest-point, and hit-test helpers.
- [ ] Add regression maps for pathological geometry.

## Map IO And Formats

- [ ] Complete Doom-format map loader parity.
  - [x] Skip invalid and zero-length Doom-format linedefs during load.
  - [x] Materialize Doom-format sidedefs from valid linedef references instead of as standalone orphans.
- [ ] Complete Doom-format map writer parity.
- [ ] Complete Hexen-format map loader parity.
  - [x] Skip invalid and zero-length Hexen-format linedefs during load.
  - [x] Materialize Hexen-format sidedefs from valid linedef references instead of as standalone orphans.
- [ ] Complete Hexen-format map writer parity.
- [ ] Complete UDMF map loader parity.
  - [x] Skip invalid, zero-length, and near-zero UDMF linedefs during load.
  - [x] Skip UDMF sidedefs with invalid sector references while preserving original sidedef indices.
  - [x] Normalize UDMF `moreids` tags by skipping zero and duplicate ids.
  - [x] Materialize UDMF sidedefs from valid linedef references instead of as standalone orphans.
  - [x] Preserve unknown top-level UDMF collections through load, clone, undo, and write.
- [ ] Complete UDMF map writer parity.
  - [x] Emit UDMF element blocks in UDB order.
- [ ] Port universal map stream reader behavior.
  - [x] Add stream-level UDMF reader facade with parser diagnostics.
- [ ] Port universal map stream writer behavior.
  - [x] Add stream-level UDMF writer facade with optional namespace emission.
- [ ] Port map format conversion behavior.
  - [x] Translate Doom thing flags through UDMF when converting to Hexen format.
  - [x] Clear action arguments when converting to Doom format.
  - [x] Convert Hexen `Line_SetIdentification` specials to UDMF line ids and flags.
  - [x] Convert Hexen line-id action args to UDMF ids for supported hardcoded specials.
  - [x] Convert UDMF line ids back to Hexen args for supported hardcoded specials.
  - [x] Clear UDMF-only element data when converting to binary formats.
- [ ] Port clipboard stream behavior to match UDB exactly.
  - [x] Skip invalid and zero-length clipboard linedefs and remove unreferenced pasted sidedefs.
- [ ] Port lump ordering rules.
  - [x] Order saved map blocks by configured `maplumpnames` when a game configuration is supplied.
- [ ] Port map lump metadata and node-build lump handling.
  - [x] Parse UDB `scriptbuild` map lump metadata.
  - [x] Add UDB-style bounded find/remove helpers for configured map lumps.
  - [x] Add UDB-style required nodebuilder lump completeness checks.
  - [x] Add UDB-style temporary-map cleanup for nodebuilder-generated lumps.
  - [x] Add UDB-style typed map-lump copying for required, blind-copy, nodebuilder, and script lumps.
- [ ] Port WAD map discovery behavior.
  - [x] Add config-aware discovery with required and forbidden map lump checks.
  - [x] Avoid non-map marker-name collisions when reading map sub-lumps.
  - [x] Filter config-aware discovery by configured map name format.
- [ ] Port save behavior for replacing, inserting, and renaming maps.
  - [x] Avoid replacing non-map lumps that share the target map name.
  - [x] Remove duplicate target map blocks before writing replacements.
  - [x] Create missing required config map lumps during save.
  - [x] Add collision-safe map marker renaming.
  - [x] Preserve configured script and blind-copy map lumps during save-back.
- [ ] Add round-trip tests using maps from Doom, Doom II, Heretic, Hexen, Boom, MBF, ZDoom, and GZDoom formats.

## Resource And Data Management

- [ ] Port UDB `DataManager` behavior.
- [ ] Port WAD reader resource behavior.
- [ ] Port PK3 reader resource behavior.
- [ ] Port structured PK3 reader behavior.
- [ ] Port directory reader behavior.
- [ ] Port data location and data location list behavior.
  - [x] Add UDB-style resource display names for data locations.
  - [x] Honor PK3 root texture and root flat resource options.
  - [x] Honor WAD strict patch resource option for classic texture composition.
- [ ] Port resource priority and override behavior completely.
- [ ] Port lazy and threaded resource loading.
- [ ] Port palette and colormap behavior fully.
  - [x] Resolve the active main `COLORMAP` through the resource stack.
  - [x] Resolve named colormap resources from WAD lumps and PK3 `colormaps/` folders.
  - [x] Include folder colormap resources in texture name enumeration.
- [ ] Port patch name behavior fully.
  - [x] Load root PK3 `PNAMES` for classic `TEXTURE1` and `TEXTURE2` definitions.
  - [x] Reuse nested-WAD `PNAMES` for folder-resource classic texture definitions.
- [ ] Port wall texture composition fully.
  - [x] Compose root PK3 classic `TEXTURE1` and `TEXTURE2` wall textures from patch resources.
- [ ] Port flat, sprite, patch, graphic, hires, and texture namespace behavior fully.
  - [x] Keep root PK3 flat and texture images opt-in through UDB resource options.
  - [x] Honor config-driven texture and flat namespace mixing for lookups and browsers.
- [x] Enumerate sprite frame names from WAD, PK3, directory, and TEXTURES resources.
- [ ] Port TEXTURES lump composite definitions fully.
- [ ] Port sprite offsets, rotations, and fallback behavior fully.
- [x] Port high-resolution replacements.
- [x] Port camera texture images.
- [x] Port dynamic bitmap images.
- [x] Port color and unknown image behavior.
- [x] Port voxel image behavior.
- [x] Port model resource discovery.
- [x] Port resource cache invalidation and disposal behavior.
  - [x] Clear resource caches and derived metadata when resources are cleared or disposed.
- [x] Add resource tests for WAD, PK3, nested PK3, directory resources, and mixed priority stacks.

## ZDoom And GZDoom Data

- [x] Complete ANIMDEFS parser behavior.
- [ ] Complete DECORATE parser behavior.
- [ ] Complete ZScript tokenizer behavior.
- [ ] Complete ZScript parser behavior.
- [x] Complete MAPINFO and ZMAPINFO parser behavior.
- [ ] Complete TEXTURES parser behavior.
- [x] Port MODELDEF parser.
- [ ] Port GLDEFS parser fully.
  - [x] Route `glow` wall groups into texture glow metadata.
- [x] Port SNDINFO parser.
- [x] Port SNDSEQ parser.
- [x] Port TERRAIN parser.
- [x] Port LOCKDEFS parser.
- [x] Port DECALDEF parser.
- [x] Port VOXELDEF parser.
- [x] Port IWADINFO parser.
- [x] Port CVARINFO parser.
- [x] Port REVERBS parser.
- [x] Port X11 RGB parser.
- [ ] Port actor structure behavior for DECORATE and ZScript.
- [ ] Port state, goto, and inheritance behavior.
- [ ] Port category and DoomEdNum merging behavior fully.
- [ ] Add parser fixtures from real-world mod patterns.

## Dehacked

- [x] Port Dehacked data model.
- [x] Port Dehacked thing parsing.
- [x] Port Dehacked frame parsing.
- [x] Port Dehacked parser behavior.
- [x] Integrate Dehacked data into thing definitions and editor display.
- [x] Add Dehacked parser and integration tests.

## Game Configuration

- [ ] Complete game configuration parsing parity.
  - [x] Parse universal field metadata.
  - [x] Parse thing flag comparison metadata.
  - [x] Parse things filter metadata.
  - [x] Parse damage type, internal sound, ignored directory, and ignored extension metadata.
  - [x] Parse render style, sidedef/sector flag, portal flag, brightness level, and visplane view-height metadata.
  - [x] Parse compatibility, help, thing angle, sidedef compression, and MODELDEF pitch metadata.
  - [x] Parse map format flag metadata and UDMF feature toggles.
  - [x] Parse testing, map boundary, sky flat, light level, and long texture name settings.
  - [x] Parse make-door defaults and default thing flags.
  - [x] Parse editor behavior metadata for scaled texture offsets, format interface, default activation, generalized toggles, 3D start type, activation filter, and visplane view height.
  - [x] Parse game-level default texture scales, default textures, and default sky texture mappings.
- [x] Port configuration inheritance and includes.
- [x] Port thing categories.
- [x] Port thing type info behavior.
- [x] Port linedef action categories and action info.
- [x] Port linedef activation info.
- [x] Port argument info behavior.
- [x] Port sector effect info.
- [x] Port texture sets.
- [x] Port resource texture sets.
- [x] Port matching texture sets.
- [x] Port generalized categories, options, and bits.
- [x] Port flag translations.
- [x] Port enum lists and enum items.
- [x] Port script configuration.
- [x] Port compiler info.
- [x] Port node-builder info.
- [x] Port required archive behavior.
- [x] Port static limits.
- [x] Add parity tests for bundled UDB configuration files.

## Type Handlers

- [x] Port universal type model.
- [x] Port type manager metadata registry.
- [x] Port bool handler.
- [x] Port integer and float handlers.
- [x] Port random integer and random float handlers.
- [x] Port string handler.
- [x] Port enum option handler.
- [x] Port enum bits handler.
- [x] Port enum strings handler.
- [x] Port texture and flat handlers.
- [x] Port color handler.
- [x] Port angle handlers.
- [x] Port thing type and thing class handlers.
- [x] Port thing tag and thing radius handlers.
- [x] Port linedef type and linedef tag handlers.
- [x] Port sector effect and sector tag handlers.
- [x] Port polyobject number handler.
- [x] Integrate handlers into argument property editors.
- [x] Integrate handlers into configured universal field editors.
- [ ] Integrate handlers into property editing UI.

## Editing Core

- [ ] Port editing manager behavior.
- [ ] Port edit mode lifecycle behavior.
- [ ] Port classic mode behavior.
- [ ] Port copy/paste manager behavior fully.
- [ ] Port undo manager behavior fully.
- [ ] Port undo snapshot behavior.
- [x] Port grid setup behavior.
- [ ] Port things filter behavior fully.
  - [x] Add UI-independent things filter evaluator.
  - [x] Integrate configured things filters into the existing thing filter window.
- [ ] Port custom things filters.
- [ ] Port selection operations for every edit mode.
  - [x] Add active-mode select all and invert selection.
- [ ] Port snapping behavior fully.
  - [x] Use UDB-compatible grid setup snap math in the editor.
- [ ] Port drawing behavior for sectors, lines, rectangles, ellipses, curves, and grids.
- [ ] Port texture alignment tools.
- [ ] Port stair builder behavior fully.
- [ ] Port slope editing behavior fully.
- [ ] Port sound propagation editing behavior fully.
- [ ] Port 3D floor editing behavior fully.
- [ ] Port geometry cleanup tools.
- [ ] Port map analysis and map check integration.

## 2D Editor UI

- [ ] Port main window layout parity or define a documented Avalonia replacement.
- [ ] Port menu layout and all menu actions.
- [ ] Port toolbar layout and all toolbar actions.
- [ ] Port status bar behavior.
- [ ] Port dockers and tabbed dock panels.
- [ ] Port info panels for vertices, linedefs, sidedefs, sectors, and things.
- [x] Port thing browser.
- [x] Port texture browser.
- [x] Port flat browser.
- [x] Port action browser.
- [x] Port effect browser.
- [ ] Port flags and bit flags dialogs.
  - [x] Add selected thing/linedef named UDMF flags dialog.
- [ ] Port custom fields dialog.
  - [x] Add generic selected-element custom UDMF fields dialog.
- [ ] Port map options dialog.
  - [x] Add basic map marker and UDMF namespace dialog.
- [ ] Port open map options dialog.
  - [x] Prompt for initial map selection when opening multi-map WAD or PK3 archives.
- [x] Port preferences dialog.
- [ ] Port resource options dialog.
  - [x] Add basic add-resource options for strict WAD patches and PK3 root texture/flat discovery.
- [ ] Port config dialog.
  - [x] Add bundled configuration picker with external cfg browse fallback.
- [x] Port find and replace dialog.
- [x] Port tag statistics dialog.
- [x] Port thing statistics dialog.
- [ ] Port grid setup dialog.
  - [x] Add basic grid size, origin, and rotation dialog.
- [x] Port center-on-coordinates dialog.
- [x] Port error/check results dialog.
- [x] Port exception dialog.
- [x] Port about dialog.

## Rendering

- [ ] Port renderer device behavior or document an equivalent renderer architecture.
- [ ] Port renderer 2D parity.
- [ ] Port renderer 3D parity.
- [ ] Port render passes and layers.
- [ ] Port render modes and view modes.
- [ ] Port vertex formats and buffers.
- [ ] Port index buffer behavior.
- [ ] Port texture handling and lifecycle.
- [ ] Port surface manager behavior.
- [ ] Port surface entries and updates.
- [ ] Port mesh behavior.
- [ ] Port text font and text label rendering.
- [ ] Port visual vertex handles.
- [ ] Port visual slope handles.
- [ ] Port comments rendering.
- [ ] Port color settings and color collections.
- [ ] Port shader compiler behavior or a documented replacement.
- [ ] Add screenshot and pixel-level regression tests for key render states.

## Visual Modes And 3D Editing

- [ ] Port visual mode lifecycle behavior.
- [ ] Port visual camera behavior.
- [ ] Port visual geometry behavior.
- [ ] Port visual sector behavior.
- [ ] Port visual thing behavior.
- [ ] Port visual vertex behavior.
- [ ] Port visual slope behavior.
- [ ] Port visual blockmap behavior.
- [ ] Port visual picking behavior to full UDB behavior.
- [ ] Port surface highlighting and selection behavior.
- [ ] Port floor, ceiling, wall, and thing editing behavior in 3D.
- [ ] Port texture copying, pasting, browsing, and aligning behavior in 3D.
- [ ] Port brightness editing behavior.
- [ ] Port 3D floors visual editing.
- [ ] Port slopes visual editing.
- [ ] Port model and voxel display.
- [ ] Port dynamic light and glowing flat display behavior.

## Model Loading

- [ ] Port shared model loader infrastructure.
- [ ] Port GZ model representation.
- [ ] Port MD2 loader.
- [ ] Port MD3 loader.
- [ ] Port OBJ loader.
- [ ] Port IQM loader.
- [ ] Port KVX loader.
- [ ] Port Unreal model loader.
- [ ] Port model skins and frame behavior.
- [x] Port model resource discovery from MODELDEF.
- [ ] Integrate models into 3D thing rendering.

## Script Editing And Compilers

- [ ] Port script resource model.
- [ ] Port script handlers for ACS, DECORATE, MODELDEF, and ZScript.
- [ ] Port script editor UI.
- [ ] Port script document tabs.
- [ ] Port script syntax highlighting behavior.
- [ ] Port find-usages behavior.
- [ ] Port script compile flow.
- [ ] Port ACC compiler integration.
- [ ] Port BCC compiler integration.
- [ ] Port ZT-BCC compiler integration.
- [ ] Port nodes compiler integration.
- [ ] Port compiler error parsing and display.

## Built-In Plugin Ports

- [ ] Port `BuilderModes`.
- [ ] Port `BuilderEffects`.
- [ ] Port `3DFloorMode`.
- [ ] Port `AutomapMode`.
- [ ] Port `BlockmapExplorer`.
- [ ] Port `ColorPicker`.
- [ ] Port `CommentsPanel`.
- [ ] Port `NodesViewer`.
- [ ] Port `RejectExplorer`.
- [ ] Port `SoundPropagationMode`.
- [ ] Port `StairSectorBuilder`.
- [ ] Port `TagExplorer`.
- [ ] Port `TagRange`.
- [ ] Port `UDBScript`.
- [ ] Port `USDF`.
- [ ] Port `VisplaneExplorer`.
- [ ] Port `WadAuthorMode`.
- [ ] Decide whether to port `ImageDrawingExample` or keep it as a sample plugin.

## BuilderModes Detail

- [ ] Port find and replace infrastructure.
- [ ] Port all find and replace object types.
- [ ] Port map error checker infrastructure.
- [ ] Port all map error checks.
- [ ] Port all map error result types and fix actions.
- [ ] Port draw-line, draw-rectangle, draw-ellipse, draw-curve, and draw-grid modes.
- [ ] Port sector drawing options.
- [ ] Port edit selection panel behavior.
- [ ] Port undo/redo panel behavior.
- [ ] Port select similar element behavior.
- [ ] Port texture fitting behavior.
- [ ] Port bridge mode.
- [ ] Port slope arch tools.
- [ ] Port make-door tools.
- [ ] Port object export tools.
- [ ] Port image export tools.
- [ ] Port Wavefront export tools.
- [ ] Port id Studio export tools.
- [ ] Port visual vertex slope behavior.
- [ ] Port all BuilderModes visual modes.

## UDBScript

- [ ] Port script discovery and execution.
- [ ] Port script options.
- [ ] Port script docker UI.
- [ ] Port script runner UI.
- [ ] Port script runtime constraints.
- [ ] Port script exception handling.
- [ ] Port query options.
- [ ] Port full UDBScript API wrappers for maps, vertices, linedefs, sidedefs, sectors, things, vectors, planes, blockmaps, images, data, and game config.
- [ ] Add API compatibility tests for common UDBScript scripts.

## Specialized Plugin Detail

- [ ] Port 3D floor control-sector editing.
- [ ] Port 3D floor slope editing.
- [ ] Port automap mode rendering and editing behavior.
- [ ] Port blockmap explorer UI and data model.
- [ ] Port comments panel data model and docker.
- [ ] Port nodes viewer visualization.
- [ ] Port reject explorer visualization.
- [ ] Port sound propagation mode behavior.
- [ ] Port stair sector builder mode and form behavior.
- [ ] Port tag explorer tree behavior.
- [ ] Port tag range tools.
- [ ] Port USDF tools.
- [ ] Port visplane explorer analysis.
- [ ] Port WadAuthor mode behavior.
- [ ] Port color picker controls and dialogs.

## Validation And Test Strategy

- [ ] Add unit tests for every parser and serializer.
- [ ] Add round-trip tests for every supported map format.
- [ ] Add golden-file tests for WAD output where deterministic output is expected.
- [ ] Add parser regression tests from representative real-world mods.
- [ ] Add property tests for geometry operations.
- [ ] Add editor-model tests for undo and redo.
- [ ] Add UI smoke tests for opening, editing, saving, and reopening maps.
- [ ] Add rendering smoke tests for 2D and 3D views.
- [ ] Add plugin lifecycle tests.
- [ ] Add source-port launch tests with mocked process execution.
- [ ] Add performance tests for large maps and large resource stacks.
- [ ] Add compatibility tests against selected UDB behavior where exact parity matters.

## Packaging And Distribution

- [ ] Define supported operating systems.
- [ ] Create app packaging for macOS.
- [ ] Create app packaging for Windows.
- [ ] Create app packaging for Linux.
- [ ] Bundle or locate default configuration assets.
- [ ] Bundle icons and app metadata.
- [ ] Define external dependency discovery for source ports, node builders, and compilers.
- [ ] Add release build scripts.
- [ ] Add smoke tests for packaged builds.
- [ ] Add migration documentation for UDB users.

## Documentation

- [ ] Document current feature support.
- [ ] Document unsupported UDB features.
- [ ] Document project architecture.
- [ ] Document map IO behavior.
- [ ] Document resource loading behavior.
- [ ] Document plugin API.
- [ ] Document testing strategy.
- [ ] Document contribution workflow.
- [ ] Document manual QA scenarios.
- [ ] Document release process.

## Release Criteria For Full Port

- [ ] Every UDB core source folder is marked `ported` or has an explicitly documented replacement.
- [ ] Every bundled UDB plugin is marked `ported`, intentionally omitted, or replaced with equivalent behavior.
- [ ] Editor app builds from the main solution.
- [ ] All automated tests pass on supported platforms.
- [ ] Representative maps from Doom, Doom II, Heretic, Hexen, Boom, MBF, ZDoom, and GZDoom load, edit, save, and reload correctly.
- [ ] Representative resource stacks using IWAD, PWAD, PK3, and directory resources render correctly.
- [ ] 2D editing workflows are usable without falling back to UDB.
- [ ] 3D visual editing workflows are usable without falling back to UDB.
- [ ] Plugin workflows are usable or intentionally documented as not in scope.
- [ ] Packaged builds launch and can open, edit, save, and test a map.
