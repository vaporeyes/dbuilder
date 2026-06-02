# Rendering Architecture

This document records DBuilder's current renderer replacement for Ultimate Doom Builder's native OpenGL path. It is a parity baseline, not a claim that all rendering behavior is complete.

## Current Replacement

DBuilder uses `DBuilder.Rendering` with Silk.NET OpenGL. The editor gets an OpenGL context through Avalonia, while render spike and demo hosts use standalone Silk.NET windows. This replaces UDB's native OpenGL device layer with a cross-platform managed binding.

The current renderer contract is also represented by `RendererArchitectureModel.Current` so docs and tests can agree on the same baseline.

## Shader Compiler Replacement

UDB's native `GLShader` and `GLShaderManager` path is currently replaced by direct `DBuilder.Rendering.Shader` instances. `Shader` compiles vertex and fragment GLSL at runtime through Silk.NET, links the program, disposes temporary shader objects, deletes the program on disposal, and caches uniform locations per program.

There is no separate offline shader compiler in the current DBuilder renderer. The replacement strategy is runtime GLSL compilation against a desktop OpenGL 3.3 core profile.

## Covered Surface

- Viewport and clear state.
- Cull, depth, fill, blend, sampler, and texture state.
- Flat and world vertex-buffer attribute binding.
- Index-buffer binding and primitive draw dispatch.
- Runtime shader compile, link, disposal, and uniform lookup caching.

## Remaining Gaps

- Full UDB render-pass graph.
- Surface manager and surface-entry lifecycle.
- Full mesh behavior.
- Text font and label rendering.
- Complete visual-mode rendering parity.
