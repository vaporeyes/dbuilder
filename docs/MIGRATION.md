# Migrating From Ultimate Doom Builder

This guide is for Ultimate Doom Builder users evaluating or moving specific workflows to DBuilder during the parity port. DBuilder is not a full replacement yet. Treat this as a current capability guide and check `docs/TODO.md`, `docs/FEATURE_SUPPORT.md`, and `docs/PARITY_MATRIX.md` before relying on a workflow.

## Before You Start

- Keep Ultimate Doom Builder installed for workflows that are still marked incomplete.
- Keep backups of maps before editing them in DBuilder.
- Use development builds from this repository until packaged releases exist.
- Provide your own IWAD, PWAD, PK3, and directory resources. DBuilder does not bundle copyrighted game assets.
- Keep a local UDB checkout at `~/dev/repos/UltimateDoomBuilder` when running parity comparison tests.

## Supported Map Workflows

DBuilder can currently load, edit, save, and reopen Doom, Hexen, and UDMF maps through WAD and PK3 backed map IO. The covered workflow includes basic geometry, sectors, things, undo, redo, UDMF custom fields, and unknown UDMF editor-state blocks.

For normal source-tree usage:

```bash
dotnet run --project src/DBuilder.Editor/DBuilder.Editor.csproj
```

Use `File > Open` for WAD or PK3 resources, choose the map, edit, then save through the editor. Headless smoke coverage verifies open, edit, undo, redo, save, and reopen paths, but full UI automation is still incomplete.

## Resources And Game Configurations

DBuilder uses UDB-style configuration and resource behavior for the ported areas:

- WAD, PK3, nested WAD, nested PK3, directory resources, textures, flats, sprites, palettes, colormaps, and model resources have focused coverage.
- Game configuration parsing covers many UDB sections, including actions, effects, arguments, things filters, compiler metadata, and resource namespaces.
- Default configuration asset bundling is still open. Development builds expect assets to be available from the source tree or configured locations.

## External Tools

Source ports, node builders, and script compilers are explicit configuration rather than host auto-detection.

- Configure Test Map source-port paths in Settings or use `DBUILDER_TESTPORT` for development.
- Configure IWAD paths and extra launch arguments before using Test Map.
- Node builders and script compilers are loaded from UDB-style compiler cfg files.
- Missing source ports, compilers, nodebuilder support files, or expected generated lumps are reported as workflow errors instead of being guessed.

See `docs/PLATFORMS_AND_TOOLS.md` for the detailed discovery rules.

## Plugins And Scripts

DBuilder has ports of selected built-in UDB plugin behavior and UDBScript workflows, but it does not yet load arbitrary external UDB plugins as drop-in extensions.

Current guidance:

- Treat compiled-in plugin-style features as DBuilder features, not external plugin compatibility.
- Check `docs/PARITY_MATRIX.md` for the plugin source-area status.
- Keep UDB available for external plugin workflows until plugin API execution, settings, resource handling, and UI contribution behavior are complete.

## Known Migration Gaps

Do not migrate these workflows without checking the active TODO first:

- Full UDB application orchestration and action-manager behavior.
- Full plugin loading and external plugin compatibility.
- Complete edit-mode lifecycle, classic mode, and editing manager parity.
- Full 2D and 3D renderer parity.
- Full visual editing parity for floors, ceilings, walls, things, slopes, and 3D floors.
- Script editor UI parity.
- Packaged macOS, Windows, and Linux installation.

## Validation Checklist

Before using DBuilder as the main editor for a map:

1. Open the map and verify the selected game configuration.
2. Confirm required resources load and expected textures, flats, sprites, and models resolve.
3. Save a copy of the map.
4. Reopen the saved copy in DBuilder and verify geometry, things, sectors, custom UDMF fields, scripts, and map options.
5. Open the saved copy in Ultimate Doom Builder when the workflow depends on behavior that is still partial.
6. Run source-port testing from both editors for maps intended for release.

## Reporting Migration Issues

When reporting a migration gap, include:

- The map format and game configuration.
- The resource stack order.
- The DBuilder commit.
- The equivalent Ultimate Doom Builder behavior.
- A minimal map or synthetic reproduction when possible.

Record confirmed gaps in `docs/TODO.md` or the parity matrix so future slices can close them without mixing unrelated fixes.
