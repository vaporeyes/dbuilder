# Architecture Map

This document maps current DBuilder projects to the Ultimate Doom Builder source areas they are expected to replace or cover.

## Target

- Supported operating systems: macOS, Windows, and Linux.
- SDK target: .NET 8 via `Directory.Build.props`.
- Current local SDK used for baseline verification: .NET SDK 10.0.107.

## Solution Policy

`DBuilder.slnx` includes the editor, core libraries, demos, spikes, and tests during the port. Demo and spike projects stay in the main solution for now because they exercise rendering, texture loading, and map-viewing surfaces that do not yet have complete automated coverage.

## Project Map

| DBuilder project | UDB source areas | Notes |
| --- | --- | --- |
| `DBuilder.Editor` | `Source/Core/Actions`, `Controls`, `Editing`, `General`, `GZBuilder`, `Plugins`, `Properties`, `VisualModes`, `Windows` | Avalonia shell and editor workflows. Currently partial. |
| `DBuilder.Geometry` | `Source/Core/Geometry` | Shared vector, plane, curve, line, and frustum helpers. Partial. |
| `DBuilder.IO` | `Source/Core/Config`, `Data`, `Dehacked`, `IO`, `Resources`, `Types`, `ZDoom` | Map formats, resources, parsers, configuration, and external tooling data. Partial. |
| `DBuilder.Map` | `Source/Core/Map`, selected `Source/Core/Geometry`, selected `Source/Core/VisualModes` | Map element model, selection, analysis, editing helpers, tracing, triangulation, and picking. Partial. |
| `DBuilder.Rendering` | `Source/Core/Rendering`, `Source/Native/OpenGL` | Cross-platform Silk.NET rendering layer. Partial. |
| `DBuilder.MapDemo` | Sample coverage for `Map` and `Rendering` | Kept as a manual visual smoke target. |
| `DBuilder.MapViewer` | Sample coverage for map IO, resources, and rendering | Kept as a manual map loading and viewing target. |
| `DBuilder.RenderSpike` | Sample coverage for `Rendering` | Kept until rendering has broader automated coverage. |
| `DBuilder.TextureDemo` | Sample coverage for resource texture loading | Kept until texture composition and resource priority are fully covered. |
| `DBuilder.Tests` | Cross-project regression coverage | Baseline test suite for every phase. |

## Standard Verification

Run `scripts/verify.sh` before completing a phase. It restores, builds, and tests the solution with serialized MSBuild because the local .NET 10.0.107 SDK fails silently on this solution during parallel builds. Use `docs/parity-matrix.json` as the machine-readable parity source and `docs/PARITY_MATRIX.md` as the reviewable summary.
