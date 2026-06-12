# Feature Support

This document summarizes what DBuilder can currently do and which Ultimate Doom Builder areas are still intentionally incomplete. Use `docs/TODO.md` for slice-level work tracking and `docs/PARITY_MATRIX.md` for source-area parity status.

## Current Support

### Editor Shell

- Avalonia editor window with File, Edit, View, Draw, Tools, Preferences, and Help command surfaces.
- Open, create, save, save-as, close, reload, recent file, recent map, autosave, recovery, and dirty-state workflows.
- Preferences, game configuration selection, map options, resource options, grid setup, command palette, status history, shortcut reference, and selected info panels.
- Non-modal windows for map checking, tag statistics, thing statistics, comments, blockmap, nodes, reject, scripts, undo/redo, and selected plugin-style tools where ported.

### Map And Resource IO

- Doom, Hexen, and UDMF map loading and writing through WAD, PK3, embedded-WAD, directory, and clipboard-style streams.
- Configured map-lump ordering, script and blind-copy lump preservation, map marker renaming, nodebuilder lump checks, and save-back protection for non-map lumps.
- WAD, PK3, nested PK3, directory, mixed-stack, texture, flat, sprite, palette, colormap, TEXTURES, MODELDEF, VOXELDEF, and model skin resource behavior with focused regression coverage.

### Editing Workflows

- Core selection, selection groups, copy, cut, paste, paste special, duplicate, paste-properties, property editing, custom fields, thing filtering, grid snapping, grid transforms, and geometry cleanup.
- 2D drawing for sectors, lines, rectangles, ellipses, curves, grids, selected-linedef curves, thing placement from generated geometry, make-sector, split-linedef, and traced sector creation.
- BuilderModes-style find/replace, map analysis, map-check result actions, select-similar, edit-selection transforms, undo/redo panel, texture alignment, flat alignment, gradients, bridge mode, door builder, and selected stair-builder behavior.

### Rendering And Visual Mode

- 2D map rendering with classic view modes, brightness and texture modes, full-brightness toggle, highlight toggle, fixed thing scale, thing marker collapse, comment icons, dynamic light tinting, glowing flats, and color collections.
- Early visual-mode support for camera actions, culling, picking, alpha-based middle texture and 3D floor picking, selected surface operations, visual thing operations, visual texture operations, slopes, resolved 3D floors, MODELDEF mesh batches, KVX voxel batches, and billboard fallback.
- Renderer architecture uses a cross-platform Silk.NET OpenGL replacement instead of UDB's native renderer. See `docs/RENDERING.md`.

### Data, Scripts, And Plugins-In-Progress

- Game configuration parsing for many UDB config sections, universal field handlers, action and effect metadata, argument metadata, things filters, and script compiler metadata.
- DECORATE, ZScript, MAPINFO, ZMAPINFO, MODELDEF, TEXTURES, GLDEFS, SNDINFO, SNDSEQ, TERRAIN, LOCKDEFS, DECALDEF, VOXELDEF, IWADINFO, CVARINFO, REVERBS, and related parser coverage.
- Script resource identity, script type detection, navigator entries, syntax highlighting metadata, find-usages, compile planning, compiler error parsing, and compiler output routing.
- Plugin host API planning, lifecycle hook modeling, contribution modeling, settings planning, reflection diagnostics, and callback catalog metadata are documented in `docs/PLUGIN_API.md`.
- Built-in plugin ports for selected BuilderEffects, 3DFloorMode, AutomapMode, BlockmapExplorer, ColorPicker, CommentsPanel, NodesViewer, RejectExplorer, SoundPropagationMode, StairSectorBuilder, TagExplorer, TagRange, UDBScript, USDF, VisplaneExplorer, WadAuthorMode, and ImageDrawingExample behavior.

## Known Unsupported Or Incomplete UDB Features

### Application And Plugin Framework

- Full UDB `General` orchestration and action-manager parity remain incomplete.
- A real plugin loading architecture, full plugin API execution, full plugin resource handling, full plugin settings persistence, full plugin menu and toolbar contribution, and full plugin error isolation are not implemented.
- Ported plugin behavior is currently compiled into DBuilder rather than loaded as external UDB-compatible plugins.

### Full Editing And Visual Parity

- MapSet, geometry, sector builder, curve interpolation, triangulation edge cases, editing manager, edit-mode lifecycle, classic mode, undo snapshots, stairs, slopes, sound propagation, and 3D floor editing are still partial.
- Full 2D renderer parity, full 3D renderer parity, render passes, vertex and index buffer behavior, texture lifecycle, surface manager, mesh behavior, text labels, screenshot regression coverage, script editor UI, and full visual mode workflows remain incomplete.
- Some BuilderModes and plugin-style modes have model and command coverage but not full docked UDB workflow parity.

### Data Coverage And Compatibility

- Full UDB DataManager behavior, lazy and threaded resource loading, complete parser fixtures from real-world mods, and full parser and serializer coverage remain open.
- Map-format round-trips, deterministic WAD golden-file tests, large-map performance tests, and selected UDB compatibility tests have focused validation coverage.
- Representative copyrighted IWAD and PWAD assets are not committed. Tests use synthetic fixtures and repo-owned data.

### Packaging And Release

- Packaged macOS, Windows, and Linux builds are not complete.
- Default configuration asset bundling, app icons and metadata, release scripts, and packaged smoke tests remain open.
- Automatic update checking is intentionally replaced by `docs/UPDATE_POLICY.md` until packaged release artifacts exist.

## How To Read This Status

- A feature listed as supported means DBuilder has an intentional implementation and focused tests or editor wiring for the named behavior.
- A feature listed as incomplete means it must not be treated as a finished UDB replacement yet.
- `docs/TODO.md` is the authority for remaining work. `docs/PARITY_MATRIX.md` is the authority for source-area status.
