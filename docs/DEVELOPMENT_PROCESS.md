# Development Process

This document defines the current DBuilder testing strategy, contribution workflow, manual QA expectations, and release process. DBuilder is still an active Ultimate Doom Builder port, so release steps describe the current source-tree process and the requirements future packaging must satisfy.

## Testing Strategy

DBuilder uses layered verification:

- Focused tests cover each parity slice before the full suite runs.
- `bash scripts/verify.sh` is the required repository gate before each commit.
- `docs/TODO.md` records completed and remaining parity work.
- `docs/PARITY_MATRIX.md` records source-area status against Ultimate Doom Builder.
- Synthetic fixtures and repo-owned test data are preferred because copyrighted IWAD, PWAD, and PK3 assets are not committed.

Run focused tests when a slice touches a narrow area:

```bash
dotnet test tests/DBuilder.Tests/DBuilder.Tests.csproj --filter "MapSearchTests|ConfiguredMapSearchTests"
```

Run the full gate before committing:

```bash
bash scripts/verify.sh
```

The verification script restores, builds, and runs the test suite. Existing warnings must be understood before they are ignored. New warnings introduced by a slice should be fixed in that slice.

## Contribution Workflow

Work in small, verifiable slices:

1. Pick an open item from `docs/TODO.md`.
2. Compare behavior against the local Ultimate Doom Builder checkout when behavior parity is uncertain.
3. Add or identify focused tests that prove the behavior.
4. Make the smallest scoped implementation or documentation change that advances parity.
5. Run focused tests and `bash scripts/verify.sh`.
6. Update `docs/TODO.md`, `docs/PARITY_MATRIX.md`, or supporting docs when the parity state changes.
7. Commit the verified slice and push `main`.

Keep unrelated changes out of the slice. If a separate gap is discovered, record it in the tracker instead of folding it into the active change.

## Manual QA Scenarios

Manual QA is not a replacement for automated tests. Use it to cover editor workflows that are difficult to assert in unit tests.

Baseline source-tree scenarios:

- Start the editor with `dotnet run --project src/DBuilder.Editor/DBuilder.Editor.csproj`.
- Open a small WAD map and confirm the title, status bar, mode, snap, and coordinate fields update.
- Switch vertex, linedef, sector, thing, and visual modes.
- Draw a small sector, save to a temporary WAD, reload it, and confirm geometry remains valid.
- Open Preferences, Map Options, Resource Options, Grid Setup, Shortcuts, Find and Replace, and Map Check windows.
- Exercise copy, paste, undo, redo, selection groups, and Draw menu commands on a synthetic map.
- Use Test Map only when a source port and IWAD path are configured.

Rendering and resource scenarios:

- Load a map with textures, flats, sprites, and common PK3 or directory resources supplied locally.
- Toggle classic 2D view modes, full brightness, fixed thing scale, and visible grid.
- Enter visual mode and check camera movement, surface highlighting, thing billboards, resolved 3D floor display, and model or voxel fallbacks where resources are available.

Package scenarios remain open until packaged builds exist for macOS, Windows, and Linux.

## Release Process

The current release process is source-tree based:

1. Ensure `main` is clean.
2. Run `bash scripts/verify.sh`.
3. Review `docs/TODO.md`, `docs/PARITY_MATRIX.md`, and `docs/FEATURE_SUPPORT.md` for accurate status.
4. Confirm no copyrighted game assets are added.
5. Confirm visible editor changes have updated README screenshots when needed.
6. Tag only after the verified state and docs agree.

Future packaged releases must add:

- macOS, Windows, and Linux packaging outputs.
- App metadata, icons, and license inclusion.
- Packaged launch smoke tests.
- Platform-specific install and external-tool setup documentation.
- A documented update policy for signed release artifacts.

Until those tasks are complete, DBuilder should be treated as a development build rather than a finished UDB replacement.
