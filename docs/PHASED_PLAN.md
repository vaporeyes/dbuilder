# Phased Full Port Plan

This plan sequences the full Ultimate Doom Builder port into phases where each phase builds on the previous one and leaves the project in a working state.

The port direction changed after this plan was drafted: new implementation work continues in the modern C#/.NET/Avalonia codebase.

## Planning Assumptions

- Full parity means UDB core plus bundled plugins unless a feature is explicitly marked out of scope later.
- New implementation work is C#/.NET-first in the current solution.
- The current C#/.NET/Avalonia implementation is the primary port target.
- Each phase must keep existing tests passing before the next phase starts.
- Each phase should add tests for new behavior before or alongside implementation.
- Each phase should avoid unrelated refactors.
- The editor must become part of the standard build early so regressions are visible.

## Phase 0: Baseline And Tracking

Goal: Make the current state measurable and repeatable.

Deliverables:

- [x] Add `DBuilder.Editor` to `DBuilder.slnx`.
- [x] Decide which demo and spike projects remain in the main solution.
- [x] Add a machine-readable parity matrix for UDB core folders and plugins.
- [x] Add a short architecture map from UDB source areas to DBuilder projects.
- [x] Add CI or a local script that builds all production projects and runs all tests.
- [x] Record supported OS targets and .NET SDK version.

Verification:

- [x] `dotnet build DBuilder.slnx` builds the editor and core projects.
- [x] `dotnet test DBuilder.slnx` passes.
- [x] Parity matrix has every UDB core folder and bundled plugin listed.

Exit Criteria:

- The team can tell what is missing without rereading the source tree.
- The editor build is no longer a separate manual step.

## Phase 1: Core Map Model Hardening

Goal: Make the map data model strong enough to support full editing, IO, rendering, and plugins.

Deliverables:

- [ ] Expand `MapSet` toward UDB behavior for indexing, selection, marking, and element lifecycle.
- [ ] Complete UDMF field behavior for vertices, linedefs, sidedefs, sectors, and things.
- [ ] Implement typed field and argument access patterns.
- [ ] Implement group metadata and map options.
- [ ] Close gaps in sidedef part semantics.
- [ ] Complete join, merge, split, stitch, cleanup, and orphan handling.
- [ ] Add behavior-compatible selection collections and element lookup helpers.

Verification:

- [ ] Unit tests cover element add, remove, clone, dispose, select, mark, split, join, merge, and field round trips.
- [ ] Existing IO tests still pass.
- [ ] Representative malformed maps do not crash loaders or the editor.

Exit Criteria:

- Higher layers can rely on stable map semantics instead of compensating for missing model behavior.

## Phase 2: Geometry And Map Analysis Parity

Goal: Port enough geometry behavior to make editing, triangulation, checks, rendering, and visual modes reliable.

Deliverables:

- [ ] Compare every `DBuilder.Geometry` type with UDB and close behavior gaps.
- [ ] Port missing helpers from UDB `Geometry/Tools.cs`.
- [ ] Complete trace path behavior for linedefs and sidedefs.
- [ ] Complete curve, interpolation, snapping, hit-test, nearest-point, and intersection helpers.
- [ ] Harden triangulation against UDB-compatible pathological sectors.
- [ ] Expand map analysis behavior for unclosed sectors, invalid references, overlaps, and limits.

Verification:

- [ ] Geometry unit tests cover normal, degenerate, and pathological cases.
- [ ] Property-style tests cover transformations and intersections.
- [ ] A geometry regression fixture set renders and triangulates consistently.

Exit Criteria:

- Editing tools can be implemented without duplicating ad hoc geometry logic.

## Phase 3: Map IO And Round Trip Compatibility

Goal: Make DBuilder trustworthy for loading, saving, and converting maps.

Deliverables:

- [ ] Complete Doom map loader and writer parity.
- [ ] Complete Hexen map loader and writer parity.
- [ ] Complete UDMF loader and writer parity.
- [ ] Port universal stream reader and writer behavior.
- [ ] Complete map format conversion behavior.
- [ ] Complete clipboard stream behavior.
- [ ] Implement lump ordering, node-build lump handling, and map replacement rules.
- [ ] Implement save, save-as, rename, and insert-map workflows.

Verification:

- [ ] Round-trip tests cover Doom, Doom II, Heretic, Hexen, Boom, MBF, ZDoom, and GZDoom samples.
- [ ] Golden output tests cover deterministic save cases.
- [ ] Map conversion tests preserve geometry and supported fields.

Exit Criteria:

- A user can load, edit, save, and reopen representative maps without falling back to UDB.

## Phase 4: Resource And Data Manager Parity

Goal: Bring resource resolution close enough to UDB for real projects.

Deliverables:

- [ ] Port UDB `DataManager` behavior or equivalent DBuilder service.
- [ ] Complete WAD, PK3, nested PK3, and directory resource behavior.
- [ ] Complete resource priority, override, cache invalidation, and disposal behavior.
- [ ] Complete palette, colormap, PNAMES, TEXTUREx, TEXTURES, flat, sprite, patch, graphic, and hires resolution.
- [ ] Complete sprite offsets, rotations, and fallback behavior.
- [ ] Add lazy and threaded resource loading.
- [ ] Add model and voxel resource discovery hooks.

Verification:

- [x] Resource stack tests cover IWAD plus PWAD plus PK3 plus directory resources.
- [x] Texture and sprite tests cover priority overrides and namespace conflicts.
- [ ] Large resource stacks remain responsive under measured thresholds.

Exit Criteria:

- Real mod resource stacks resolve textures, flats, sprites, and graphics predictably.

## Phase 5: Game Configuration And Type System

Goal: Support UDB configuration files and typed property editing.

Deliverables:

- [ ] Complete game configuration parsing, inheritance, includes, and defaults.
- [ ] Complete thing categories, thing type info, linedef actions, sector effects, flags, enums, args, and generalized types.
- [ ] Complete compiler, node-builder, script, archive, and static limit config sections.
- [ ] Port universal type model and type manager.
- [ ] Port bool, integer, float, random, string, enum, texture, flat, color, angle, thing, linedef, sector, tag, and polyobject handlers.
- [ ] Integrate type handlers into property editing UI.

Verification:

- [ ] All bundled UDB configuration files parse.
- [ ] Config parity tests compare selected entries with UDB expectations.
- [ ] Property dialogs use typed handlers rather than raw string editing where applicable.

Exit Criteria:

- The editor can open UDB game configurations and present map properties with correct names, choices, flags, and validation.

## Phase 6: ZDoom, GZDoom, And Dehacked Data

Goal: Make modern source-port metadata available to resources, config, rendering, and editing.

Deliverables:

- [ ] Complete ANIMDEFS, DECORATE, ZScript, MAPINFO, ZMAPINFO, TEXTURES, and GLDEFS behavior.
- [ ] Port MODELDEF, SNDINFO, SNDSEQ, TERRAIN, LOCKDEFS, DECALDEF, VOXELDEF, IWADINFO, CVARINFO, REVERBS, and X11 RGB parsers.
- [ ] Complete actor structure, inheritance, states, gotos, categories, and DoomEdNum merging.
- [ ] Port Dehacked data, thing, frame, and parser behavior.
- [ ] Integrate parsed metadata into resource lookup, thing definitions, rendering, and editor UI.

Verification:

- [ ] Parser fixture tests cover common real-world mod syntax.
- [ ] Actor merge tests cover DECORATE, ZScript, MAPINFO, and Dehacked interactions.
- [ ] Editor thing browser displays parsed mod actors correctly.

Exit Criteria:

- Modern GZDoom projects expose their editor metadata correctly.

## Phase 7: Editing Core And 2D Workflows

Goal: Reach functional parity for common 2D map editing workflows.

Deliverables:

- [ ] Port editing manager and edit mode lifecycle.
- [ ] Port classic mode behavior.
- [ ] Complete undo snapshots and undo manager behavior.
- [ ] Complete copy, paste, prefab, and clipboard behavior.
- [ ] Complete grid setup and snapping behavior.
- [ ] Complete thing filters and custom filters.
- [ ] Complete selection workflows for vertices, linedefs, sectors, and things.
- [ ] Complete drawing tools for lines, sectors, rectangles, ellipses, curves, and grids.
- [ ] Complete texture alignment, stair builder, slope editing, sound propagation, 3D floor helpers, and geometry cleanup.

Verification:

- [ ] Editor-model tests cover each editing workflow without UI.
- [ ] UI smoke tests cover open, select, edit, undo, redo, save, and reload.
- [ ] Manual QA checklist covers common Doom Builder workflows.

Exit Criteria:

- A user can perform normal 2D editing tasks without switching back to UDB.

## Phase 8: UI Shell, Dialogs, And Dockers

Goal: Build the UI surface needed for full editor workflows.

Deliverables:

- [ ] Complete main window menus, toolbars, status bar, and command availability.
- [ ] Implement dockers and tabbed dock panels.
- [ ] Complete info panels for all map element types.
- [ ] Port thing, texture, flat, action, effect, and map browsers.
- [ ] Port flags, bit flags, custom fields, map options, open map options, preferences, resources, config, find and replace, tags, statistics, grid setup, errors, exception, and about dialogs.
- [ ] Add settings persistence for UI layout and editor preferences.

Verification:

- [ ] UI smoke tests open every major dialog.
- [ ] Dialog tests validate reading and writing map/config data.
- [ ] Keyboard and menu workflows match configured actions.

Exit Criteria:

- The editor exposes the core features through discoverable UI rather than only keyboard shortcuts or internal APIs.

## Phase 9: Rendering And Visual Modes

Goal: Close the gap between the current render spike and UDB's 2D/3D editing experience.

Deliverables:

- [ ] Complete 2D renderer behavior, layers, labels, handles, comments, overlays, and selection visuals.
- [ ] Complete 3D renderer behavior, surface buckets, model display, voxel display, lights, glowing flats, and view modes.
- [ ] Complete visual mode lifecycle and visual camera behavior.
- [ ] Complete visual sector, thing, vertex, slope, geometry, blockmap, and picking behavior.
- [ ] Complete 3D texture copy, paste, browse, align, offset, brightness, height, slope, and 3D floor editing.
- [ ] Port model loader infrastructure and MD2, MD3, OBJ, IQM, KVX, and Unreal loaders.

Verification:

- [ ] Screenshot tests cover key 2D and 3D views.
- [ ] Pixel checks verify representative textures, flats, sprites, and model placeholders render.
- [ ] 3D interaction tests cover surface targeting and edit operations at the model level.

Exit Criteria:

- Visual editing is usable for real maps, including textured 3D inspection and common surface edits.

## Phase 10: Compilers, Source Ports, And External Tools

Goal: Support the external toolchain workflows UDB users expect.

Deliverables:

- [ ] Port source-port launch configuration.
- [ ] Port test-map command behavior.
- [ ] Port node-builder discovery and execution.
- [ ] Port ACC, BCC, ZT-BCC, and nodes compiler integrations.
- [ ] Port compiler error parsing and display.
- [ ] Port pre-command and post-command workflows.
- [ ] Add process execution abstraction for tests.

Verification:

- [ ] Tests use mocked process execution for command construction and error parsing.
- [ ] Manual QA confirms configured source ports and node builders launch correctly.

Exit Criteria:

- Users can build nodes, compile scripts, and test maps from DBuilder.

## Phase 11: Plugin Framework

Goal: Provide the foundation needed to port bundled UDB plugins.

Deliverables:

- [ ] Implement plugin discovery and loading.
- [ ] Implement plugin lifecycle hooks.
- [ ] Implement plugin APIs for maps, resources, commands, UI, dockers, settings, and edit modes.
- [ ] Implement plugin menu and toolbar contributions.
- [ ] Implement plugin error isolation and reporting.
- [ ] Add plugin compatibility test fixtures.

Verification:

- [ ] A minimal sample plugin can load, add a command, add UI, read the map, and persist settings.
- [ ] Plugin failure tests do not crash the editor.

Exit Criteria:

- Bundled plugins can be ported without special-case integration into the editor core.

## Phase 12: Bundled Plugin Port Wave 1

Goal: Port plugins that depend primarily on core map, UI, analysis, and rendering behavior.

Deliverables:

- [ ] Port `BuilderModes` find and replace infrastructure.
- [ ] Port `BuilderModes` error checker infrastructure and all checks.
- [ ] Port `BuilderModes` draw and edit panels.
- [ ] Port `TagExplorer`.
- [ ] Port `TagRange`.
- [ ] Port `CommentsPanel`.
- [ ] Port `BlockmapExplorer`.
- [ ] Port `NodesViewer`.
- [ ] Port `RejectExplorer`.

Verification:

- [ ] Plugin tests cover lifecycle and main workflows.
- [ ] Manual QA covers each plugin's primary user path.

Exit Criteria:

- The most editor-centric bundled plugins are usable through the plugin framework.

## Phase 13: Bundled Plugin Port Wave 2

Goal: Port specialized editing, visualization, and scripting plugins.

Deliverables:

- [ ] Port `3DFloorMode`.
- [ ] Port `AutomapMode`.
- [ ] Port `BuilderEffects`.
- [ ] Port `ColorPicker`.
- [ ] Port `SoundPropagationMode`.
- [ ] Port `StairSectorBuilder`.
- [ ] Port `UDBScript`.
- [ ] Port `USDF`.
- [ ] Port `VisplaneExplorer`.
- [ ] Port `WadAuthorMode`.
- [ ] Decide whether `ImageDrawingExample` remains a sample or ships as a plugin.

Verification:

- [ ] Plugin-specific tests cover primary algorithms.
- [ ] UDBScript API compatibility tests cover common script examples.
- [ ] Manual QA covers all specialized workflows.

Exit Criteria:

- Bundled plugin coverage is complete or intentionally documented as out of scope.

## Phase 14: Performance, Stability, And Large Project QA

Goal: Make the port usable on large maps and large resource stacks.

Deliverables:

- [ ] Add performance benchmarks for map load, save, render rebuilds, resource lookup, and large selections.
- [ ] Profile large real-world maps and resource stacks.
- [ ] Optimize hot paths found by profiling.
- [ ] Add cancellation or progress reporting for long operations.
- [ ] Add memory lifecycle checks for GL resources, resource caches, and loaded archives.
- [ ] Add crash recovery and autosave validation.

Verification:

- [ ] Large maps remain interactive under documented thresholds.
- [ ] Repeated open/edit/save/close cycles do not leak significant memory.
- [ ] Long operations report progress or stay cancellable.

Exit Criteria:

- The editor is stable enough for extended editing sessions on real projects.

## Phase 15: Packaging, Documentation, And Release

Goal: Ship a complete, documented, installable editor.

Deliverables:

- [ ] Create macOS package.
- [ ] Create Windows package.
- [ ] Create Linux package.
- [ ] Bundle app icons and metadata.
- [ ] Bundle or discover default configuration assets.
- [ ] Document installation and migration from UDB.
- [ ] Document supported and unsupported features.
- [ ] Document plugin API.
- [ ] Document manual QA scenarios.
- [ ] Document release process.

Verification:

- [ ] Packaged builds launch on every supported platform.
- [ ] Packaged builds can open, edit, save, reopen, and test a representative map.
- [ ] Documentation is enough for a new user to install and configure the editor.

Exit Criteria:

- The project is ready for full-port release testing.

## Dependency Order Summary

1. Baseline and tracking.
2. Map model.
3. Geometry and analysis.
4. Map IO.
5. Resources and data manager.
6. Configuration and type handlers.
7. ZDoom, GZDoom, and Dehacked data.
8. Editing core and 2D workflows.
9. UI shell and dialogs.
10. Rendering and visual modes.
11. External tools and compilers.
12. Plugin framework.
13. Bundled plugins.
14. Performance and stability.
15. Packaging and release.

## Per-Phase Working Rules

- Start each phase by selecting a small vertical slice from the phase deliverables.
- Add or update tests before relying on new behavior.
- Keep the editor buildable after each merged slice.
- Update `docs/TODO.md` and the parity matrix as items are completed.
- Do not start plugin ports before the plugin framework is stable.
- Do not optimize before benchmarks identify a specific bottleneck.
- Do not mark a phase complete while known regressions remain.
