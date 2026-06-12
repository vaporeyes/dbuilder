# Platforms And External Tools

This document defines the current supported operating-system target and the discovery rules for external tools used by DBuilder. Packaging is still incomplete, so this is a development and release-target policy rather than an installer guide.

## Supported Operating Systems

DBuilder targets the same cross-platform desktop set throughout the port:

- macOS on Apple Silicon and Intel hardware supported by .NET 8 and Avalonia.
- Windows on x64 hardware supported by .NET 8 and Avalonia.
- Linux on x64 desktop environments supported by .NET 8 and Avalonia.

The source tree currently verifies on macOS in the local development environment. Windows and Linux are target platforms, but packaged launch validation and platform-specific smoke tests remain open until packaging is implemented.

## Runtime Requirements

- .NET 8 runtime for running built binaries.
- A recent .NET SDK for local development and verification.
- OpenGL support compatible with the current Silk.NET renderer path.
- Access to game assets supplied by the user. Copyrighted IWAD or PWAD files are not bundled.
- Optional external tools for Test Map, node building, and script compilation.

## Source Port Discovery

Source ports are configured explicitly instead of auto-detected from the host system.

- The Settings dialog stores the Test Map source port path, IWAD path, and extra arguments.
- `DBUILDER_TESTPORT` can supply a test source port fallback for development and tests.
- Map options can define before and after commands for test-map workflows.
- Missing source-port configuration blocks Test Map and reports a warning instead of guessing an executable.

This keeps launch behavior deterministic across platforms and avoids host-specific path probing while the packaged app layout is still unsettled.

## Node Builder Discovery

Node builders are discovered from UDB-style compiler configuration files.

- DBuilder reads compiler and nodebuilder cfg files recursively from the configured compiler directory.
- Nodebuilder entries are kept only when their referenced compiler is resolvable.
- Resolved nodebuilders are sorted by name for stable UI presentation.
- Compiler support files listed by the nodebuilder compiler are staged before execution.
- Missing required support files fail the nodebuilder plan before launch.
- Generated map output must contain required nodebuilder lumps before DBuilder accepts it.

The editor can use game configuration defaults for save and test nodebuilders, with per-map and settings-level overrides where the current UI exposes them.

## Script Compiler Discovery

Script compilers use the same UDB-style compiler configuration source as nodebuilders.

- Compiler definitions preserve the first loaded compiler with a duplicate name.
- ACC, BCC, ZT-BCC, and nodes compiler interfaces have dedicated argument and diagnostic handling.
- Map options can choose a compiled ACS script compiler when the active configuration supports script build metadata.
- Compiler paths and include files are resolved from the compiler configuration directory.
- Compiler initialization, process start, file operation, missing output, and diagnostic parsing errors are reported as script compiler errors.

## Release Build Script

`bash scripts/release-build.sh` publishes unsigned editor outputs for `osx-arm64`, `osx-x64`, `win-x64`, and `linux-x64` into `artifacts/release/<rid>`. Passing one or more runtime IDs limits the build to that target set.

The script does not sign, notarize, create installers, delete prior artifacts, or bundle copyrighted assets. Platform packages remain open work.

## App Metadata And Icon

The editor project defines the app title, product name, package description, Windows DPI manifest, and package icon metadata. The shared icon source is `assets/main.png`, which is linked into publish output as `main.png` and packed as the package icon.

## Default Configuration Assets

Packaged builds publish an `assets` tree next to the editor binaries. The editor prefers `assets/Common/Configurations` from the publish directory, then falls back to a development Ultimate Doom Builder checkout when that bundled path is absent. Compiler and script configuration discovery resolve from the same asset root.

## Release Packaging Implications

Packaged builds must preserve these rules:

- Do not bundle copyrighted game assets.
- Do not silently guess source ports, node builders, or compilers.
- Provide stable settings locations for configured tool paths.
- Keep bundled game, compiler, and script configuration assets under the published `assets` tree.
- Document platform-specific install paths once macOS, Windows, and Linux packages exist.

Open packaging work remains tracked in `docs/TODO.md`.
