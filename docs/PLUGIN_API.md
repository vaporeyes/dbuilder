# Plugin API

This document describes the plugin API surface DBuilder currently models while porting Ultimate Doom Builder plugin behavior. It is not a drop-in compatibility guarantee for external UDB plugins. Full plugin execution, UI integration, and packaged deployment remain tracked in `docs/TODO.md` and `docs/PARITY_MATRIX.md`.

## Current Scope

DBuilder's plugin host model lives in `DBuilder.IO` and is intentionally UI-independent. It currently covers:

- Plugin descriptor normalization.
- Load candidate ordering with UDB-style `Loadorder.cfg` filename precedence.
- Assembly load and plugin type discovery planning.
- Reflection-based plugin instantiation and revision compatibility filtering.
- Runtime-ready host filtering.
- Lifecycle hook planning and callback catalog metadata.
- Host API service availability planning.
- Contribution planning for actions, menus, toolbars, edit modes, dockers, and resource handlers.
- Plugin settings descriptor discovery, default merging, normalization, and write-back planning.
- Reflection execution helpers for configured actions, edit modes, dockers, UI bindings, resource handlers, callbacks, and shutdown.
- Diagnostics that isolate invalid, disabled, missing, ambiguous, or failing plugins.

The current editor still compiles selected plugin-style behavior directly into DBuilder. Arbitrary external plugin loading is not ready for user workflows.

## Descriptor Model

Plugin descriptors use `DBuilderPluginDescriptor`:

```csharp
public sealed record DBuilderPluginDescriptor(
    string Name,
    string AssemblyPath,
    bool Enabled = true,
    bool RequiresMap = false,
    IReadOnlyList<DBuilderPluginContribution>? Contributions = null);
```

Descriptor planning normalizes names and paths, keeps the first descriptor for each plugin name, filters disabled descriptors, and reports diagnostics for missing names, missing paths, and duplicates. Map-scoped plugins can be marked with `RequiresMap`, which keeps them out of ready plans until a map is open.

## Contributions

Contribution kinds are represented by `DBuilderPluginContributionKind`:

- `Action`
- `Menu`
- `Toolbar`
- `EditMode`
- `Docker`
- `ResourceHandler`

Each contribution has an id, title, optional method name, and optional bound action id. Planning keeps action, edit-mode, docker, menu, toolbar, and resource-handler contributions separate so the editor can resolve commands without assuming every plugin is valid.

Action, edit-mode, docker, menu, toolbar, and resource-handler command resolution produces diagnostics for missing ids, ambiguous ids, missing action bindings, and failed reflection execution.

## Lifecycle Hooks

DBuilder models the following lifecycle hooks:

- `Load`
- `RegisterActions`
- `RegisterHints`
- `Initialize`
- `RegisterUi`
- `RegisterEditModes`
- `RegisterDockers`
- `RegisterResourceHandlers`
- `MapOpened`
- `MapClosed`
- `MapSaved`
- `MapReconfigured`
- `ProgramReconfigured`
- `ResourcesReloaded`
- `MapNodesRebuilt`
- `Engage`
- `Disengage`
- `Dispose`

Lifecycle plans are derived from plugin descriptors and a `DBuilderPluginLifecycleRequest`. Map-scoped hooks are skipped when the plugin requires an open map and no map is available. Shutdown plans isolate dispose failures per plugin.

## UDB Callback Catalog

The reflection callback catalog tracks core UDB plugin and manager callbacks such as:

- Initialization and disposal callbacks.
- Map open, map close, map save, map set, and reconfiguration callbacks.
- Resource reload and node rebuild callbacks.
- Edit mode, copy, paste, undo, redo, preference, action, input, rendering, and highlight callbacks.

Abortable callbacks are modeled explicitly. Callback execution aggregates per-plugin diagnostics so one failing plugin does not invalidate the entire callback run.

## Host API Services

Host API service planning currently models UDB-style service names including:

- `General.Interface`
- `General.Actions`
- `General.Settings`
- `General.Colors`
- `General.Types`
- `General.Editing`
- `General.Map`
- `General.Map.Map`
- `General.Map.Data`
- `General.Map.Config`
- `General.Map.ThingsFilter`
- `General.Map.UndoRedo`
- `General.Map.VisualCamera`

Map-scoped services are unavailable until a map is open. The planner reports a host warning when map services are requested before map availability.

## Settings

Plugin settings are stored under plugin names in `Settings.PluginSettings`. The host model normalizes the store, discovers setting descriptors by reflection, merges defaults, and plans write-back without assuming every plugin provides valid descriptors.

Built-in plugin-style ports may also define typed settings adapters when UDB uses stable setting keys.

## Resource Handlers

Resource handler contributions use the same id and title model as other contributions. Resolution reports missing and ambiguous handlers and can invoke configured reflection methods when the runtime plan has an activated plugin instance.

## Current Limits

The following are not complete plugin API guarantees:

- External UDB plugins are not supported as drop-in extensions.
- Plugin UI controls are not embedded through the full UDB docking and toolbar framework.
- The editor does not yet expose a stable public SDK package for plugin authors.
- Map, resource, renderer, visual-mode, and editing-manager object wrappers are still incomplete.
- Packaged plugin discovery paths are not defined because packaged builds are still open.

Use `docs/PARITY_MATRIX.md` for current source-area status and `docs/TODO.md` for remaining plugin work.

## Validation

Plugin API planning and reflection helpers are covered by `DBuilderPluginHostModelTests`. Before changing plugin host behavior, run:

```bash
dotnet test tests/DBuilder.Tests/DBuilder.Tests.csproj --filter FullyQualifiedName~DBuilderPluginHostModelTests
```

The full repository gate remains:

```bash
bash scripts/verify.sh
```
